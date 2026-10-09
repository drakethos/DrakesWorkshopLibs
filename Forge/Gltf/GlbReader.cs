// Copied from Drakes Asset Forge (Forge/Format/Glb/GlbReader.cs) so mods get .glb models from Libs alone; keep in step with it.
using System;
using System.Collections.Generic;
using System.Text;


namespace DrakeModsLibs.Forge.Gltf;

/// <summary>A .glb file Forge can't use. The message says what's wrong and is shown to the user as-is.</summary>
internal sealed class GlbException : Exception
{
    public GlbException(string message) : base(message)
    {
    }
}

/// <summary>One triangle list, already in Unity's space (left-handed, Y up): ready to build a mesh from.</summary>
internal sealed class GlbSubmesh
{
    /// <summary>x, y, z per vertex.</summary>
    public float[] Positions { get; set; } = Array.Empty<float>();
    /// <summary>x, y, z per vertex; empty when the file has none (Unity recalculates them).</summary>
    public float[] Normals { get; set; } = Array.Empty<float>();
    /// <summary>u, v per vertex, with v flipped to Unity's bottom-left origin; empty when the file has none.</summary>
    public float[] Uvs { get; set; } = Array.Empty<float>();
    /// <summary>Three per triangle, wound for Unity.</summary>
    public int[] Indices { get; set; } = Array.Empty<int>();
    /// <summary>Index into <see cref="GlbModel.Materials"/>, or -1 for none.</summary>
    public int Material { get; set; } = -1;
    public string? Name { get; set; }
}

internal sealed class GlbMaterial
{
    public string? Name { get; set; }
    /// <summary>r, g, b, a (glTF baseColorFactor; 1s when absent).</summary>
    public float[] BaseColor { get; set; } = { 1f, 1f, 1f, 1f };
    /// <summary>The base colour texture's file bytes (PNG or JPEG), or null.</summary>
    public byte[]? BaseColorImage { get; set; }
    public string? BaseColorMimeType { get; set; }
    /// <summary>glTF alphaMode BLEND: the base colour's alpha is opacity (glass, water).</summary>
    public bool Blend { get; set; }
}

internal sealed class GlbModel
{
    public IReadOnlyList<GlbSubmesh> Submeshes { get; set; } = Array.Empty<GlbSubmesh>();
    public IReadOnlyList<GlbMaterial> Materials { get; set; } = Array.Empty<GlbMaterial>();
    /// <summary>Things skipped but not fatal (rigged skins used in rest pose, strips and fans not supported, and so on).</summary>
    public IReadOnlyList<string> Warnings { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Reads static meshes from a binary glTF 2.0 (.glb) file. Strict on purpose: every offset, count and index is
/// checked against the file before it's read, nothing is loaded from outside the file, and the limits below bound
/// what a hostile or broken file can ask for. Rigged meshes are read in their rest pose, with a warning.
/// Coordinates are converted from glTF's right-handed space to Unity's left-handed one (Z flipped, winding fixed,
/// V flipped), and node transforms are applied.
/// </summary>
internal static class GlbReader
{
    public const int MaxFileBytes = 64 * 1024 * 1024;
    public const int MaxVertices = 1 << 20;
    public const int MaxIndices = 3 << 20;
    public const int MaxImageBytes = 16 * 1024 * 1024;
    public const int MaxNodeDepth = 64;

    private const uint Magic = 0x46546C67; // "glTF"
    private const uint ChunkJson = 0x4E4F534A;
    private const uint ChunkBin = 0x004E4942;

    private const int TriangleMode = 4;

    private const int UnsignedByte = 5121;
    private const int UnsignedShort = 5123;
    private const int UnsignedInt = 5125;

    public static GlbModel Read(byte[] data)
    {
        if (data == null)
            throw new GlbException("No file.");
        if (data.Length < 12)
            throw new GlbException("This isn't a .glb file (too short).");
        if (data.Length > MaxFileBytes)
            throw new GlbException($"The model is larger than {MaxFileBytes / (1024 * 1024)} MB.");

        if (BitConverter.ToUInt32(data, 0) != Magic)
            throw new GlbException("This isn't a .glb file (wrong header). Export as glTF Binary (.glb).");
        var version = BitConverter.ToUInt32(data, 4);
        if (version != 2)
            throw new GlbException($"Only glTF 2.0 is supported (this file is version {version}).");
        if (BitConverter.ToUInt32(data, 8) != data.Length)
            throw new GlbException("The file is truncated or has extra data after the model.");

        byte[]? json = null;
        byte[]? bin = null;
        var offset = 12;
        while (offset < data.Length)
        {
            if (offset + 8 > data.Length)
                throw new GlbException("The file is truncated (a chunk header is cut off).");
            var length = BitConverter.ToUInt32(data, offset);
            var type = BitConverter.ToUInt32(data, offset + 4);
            offset += 8;
            if (length % 4 != 0 || length > data.Length - offset)
                throw new GlbException("The file is damaged (a chunk runs past the end).");
            var chunk = new byte[length];
            Array.Copy(data, offset, chunk, 0, (int)length);
            offset += (int)length;

            if (type == ChunkJson && json == null)
                json = chunk;
            else if (type == ChunkBin && bin == null)
                bin = chunk;
            // Anything else is an extension chunk, which the spec says to skip.
        }
        if (json == null)
            throw new GlbException("The file has no model description (JSON chunk).");

        JsonValue doc;
        try
        {
            doc = JsonValue.Parse(Encoding.UTF8.GetString(json).TrimEnd(' ', '\0'));
        }
        catch (FormatException ex)
        {
            throw new GlbException($"The model description is damaged: {ex.Message}");
        }
        if (!doc.IsObject)
            throw new GlbException("The model description is damaged (not an object).");

        return new Loader(doc, bin ?? Array.Empty<byte>()).Load();
    }

    private sealed class Loader
    {
        private readonly JsonValue _doc;
        private readonly byte[] _bin;
        private readonly List<string> _warnings = new();
        private readonly IReadOnlyList<JsonValue> _accessors;
        private readonly IReadOnlyList<JsonValue> _bufferViews;
        private readonly IReadOnlyList<JsonValue> _meshes;
        private readonly IReadOnlyList<JsonValue> _nodes;
        private readonly IReadOnlyList<JsonValue> _materialsJson;
        private readonly IReadOnlyList<JsonValue> _textures;
        private readonly IReadOnlyList<JsonValue> _images;
        private int _vertices;
        private int _indices;

        public Loader(JsonValue doc, byte[] bin)
        {
            _doc = doc;
            _bin = bin;
            _accessors = Arr(doc["accessors"]);
            _bufferViews = Arr(doc["bufferViews"]);
            _meshes = Arr(doc["meshes"]);
            _nodes = Arr(doc["nodes"]);
            _materialsJson = Arr(doc["materials"]);
            _textures = Arr(doc["textures"]);
            _images = Arr(doc["images"]);
        }

        public GlbModel Load()
        {
            CheckAsset();
            CheckBuffers();

            var materials = new List<GlbMaterial>();
            foreach (var m in _materialsJson)
                materials.Add(ReadMaterial(m, materials.Count));

            var submeshes = new List<GlbSubmesh>();
            foreach (var root in SceneRoots())
                Walk(root, GlbMat4.Identity, 0, new HashSet<int>(), submeshes);

            return new GlbModel
            {
                Submeshes = submeshes,
                Materials = materials,
                Warnings = _warnings
            };
        }

        private void CheckAsset()
        {
            var asset = _doc["asset"];
            var version = asset?["version"]?.AsString();
            if (version == null || !version.StartsWith("2", StringComparison.Ordinal))
                throw new GlbException("Only glTF 2.0 is supported.");

            foreach (var ext in Arr(_doc["extensionsRequired"]))
            {
                var name = ext.AsString() ?? "?";
                throw new GlbException($"The model needs the glTF extension '{name}', which Forge can't read yet. Re-export without it.");
            }
        }

        private void CheckBuffers()
        {
            var buffers = Arr(_doc["buffers"]);
            if (buffers.Count > 1)
                throw new GlbException("The model has more than one buffer. Export as a single .glb.");
            if (buffers.Count == 1)
            {
                var buffer = buffers[0];
                if (buffer["uri"] != null)
                    throw new GlbException("The model loads data from another file. Export with the data embedded (.glb).");
                if (Int(buffer["byteLength"], 0, "buffer byteLength") > _bin.Length)
                    throw new GlbException("The model's data is cut short.");
            }
        }

        private GlbMaterial ReadMaterial(JsonValue m, int index)
        {
            var baseColor = new float[] { 1f, 1f, 1f, 1f };
            byte[]? image = null;
            string? mime = null;

            var pbr = m["pbrMetallicRoughness"];
            if (pbr != null)
            {
                var factor = Arr(pbr["baseColorFactor"]);
                if (factor.Count == 4)
                    for (var i = 0; i < 4; i++)
                        baseColor[i] = Clamp01(Number(factor[i], "baseColorFactor"));

                var texture = pbr["baseColorTexture"];
                if (texture != null)
                {
                    var textureIndex = Int(texture["index"], -1, $"material {index} texture");
                    if (textureIndex >= 0)
                        (image, mime) = ReadImage(Element(_textures, textureIndex, "texture")["source"]);
                }
            }

            return new GlbMaterial
            {
                Name = m["name"]?.AsString(),
                BaseColor = baseColor,
                BaseColorImage = image,
                BaseColorMimeType = mime,
                Blend = m["alphaMode"]?.AsString() == "BLEND"
            };
        }

        private (byte[]? bytes, string? mime) ReadImage(JsonValue? sourceIndex)
        {
            if (sourceIndex == null || sourceIndex.AsNumber() is not { } n)
                return (null, null);
            var image = Element(_images, (int)n, "image");
            if (image["uri"] != null)
                throw new GlbException("A texture loads an image from another file. Export with the images embedded (.glb).");
            var viewIndex = Int(image["bufferView"], -1, "image bufferView");
            if (viewIndex < 0)
                return (null, null);
            var (start, length) = View(viewIndex, "image");
            if (length > MaxImageBytes)
                throw new GlbException("A texture is larger than 16 MB.");
            var bytes = new byte[length];
            Array.Copy(_bin, start, bytes, 0, length);
            return (bytes, image["mimeType"]?.AsString() ?? "image/png");
        }

        private IEnumerable<int> SceneRoots()
        {
            var scenes = Arr(_doc["scenes"]);
            if (scenes.Count > 0)
            {
                var scene = Int(_doc["scene"], 0, "scene");
                foreach (var node in Arr(Element(scenes, scene, "scene")["nodes"]))
                    yield return Index(node, _nodes.Count, "scene node");
                yield break;
            }

            // No scenes: every node that isn't somebody's child is a root.
            var child = new HashSet<int>();
            foreach (var node in _nodes)
                foreach (var c in Arr(node["children"]))
                    child.Add(Index(c, _nodes.Count, "child node"));
            for (var i = 0; i < _nodes.Count; i++)
                if (!child.Contains(i))
                    yield return i;
        }

        private void Walk(int nodeIndex, GlbMat4 parent, int depth, HashSet<int> onPath, List<GlbSubmesh> output)
        {
            if (depth > MaxNodeDepth)
                throw new GlbException("The model's node hierarchy is nested too deeply.");
            if (!onPath.Add(nodeIndex))
                throw new GlbException("The model's node hierarchy loops back on itself.");

            var node = _nodes[nodeIndex];
            // Row vectors: the node's own transform applies first, then its parent's.
            var world = LocalMatrix(node).Mul(parent);

            var meshIndex = Int(node["mesh"], -1, "node mesh");
            if (meshIndex >= 0)
                ReadMesh(Element(_meshes, meshIndex, "mesh"), meshIndex, world, output);

            foreach (var child in Arr(node["children"]))
                Walk(Index(child, _nodes.Count, "child node"), world, depth + 1, onPath, output);

            onPath.Remove(nodeIndex);
        }

        private GlbMat4 LocalMatrix(JsonValue node)
        {
            var matrix = Arr(node["matrix"]);
            if (matrix.Count == 16)
            {
                var a = new float[16];
                for (var i = 0; i < 16; i++)
                    a[i] = (float)Number(matrix[i], "node matrix");
                // glTF stores columns; the reader's matrices are rows, so the array reads straight across.
                return GlbMat4.FromRows(a);
            }

            var t = Vec(node["translation"], 3, new float[] { 0, 0, 0 }, "node translation");
            var s = Vec(node["scale"], 3, new float[] { 1, 1, 1 }, "node scale");
            var r = Vec(node["rotation"], 4, new float[] { 0, 0, 0, 1 }, "node rotation");
            return GlbMat4.Trs(s[0], s[1], s[2], r[0], r[1], r[2], r[3], t[0], t[1], t[2]);
        }

        private void ReadMesh(JsonValue mesh, int meshIndex, GlbMat4 world, List<GlbSubmesh> output)
        {
            var name = mesh["name"]?.AsString();
            var primitives = Arr(mesh["primitives"]);
            for (var p = 0; p < primitives.Count; p++)
            {
                var prim = primitives[p];
                var mode = Int(prim["mode"], TriangleMode, "primitive mode");
                if (mode != TriangleMode)
                {
                    _warnings.Add($"mesh {meshIndex} primitive {p}: only triangles are supported (mode {mode}), skipped.");
                    continue;
                }

                var attributes = prim["attributes"];
                if (attributes == null)
                    throw new GlbException($"Mesh {meshIndex} has no vertex data.");
                var posIndex = Int(attributes["POSITION"], -1, "POSITION");
                if (posIndex < 0)
                    throw new GlbException($"Mesh {meshIndex} has no positions.");
                if (attributes["JOINTS_0"] != null || attributes["WEIGHTS_0"] != null || prim["targets"] != null)
                    AddOnce($"'{name ?? "mesh " + meshIndex}' is rigged or animated: its rest pose is used.");

                var positions = ReadAttribute(posIndex, 3, "POSITION");
                var vertexCount = positions.Length / 3;
                _vertices += vertexCount;
                if (_vertices > MaxVertices)
                    throw new GlbException($"The model has more than {MaxVertices:N0} vertices.");

                var normalIndex = Int(attributes["NORMAL"], -1, "NORMAL");
                var uvIndex = Int(attributes["TEXCOORD_0"], -1, "TEXCOORD_0");
                var normals = normalIndex >= 0 ? ReadAttribute(normalIndex, 3, "NORMAL") : Array.Empty<float>();
                var uvs = uvIndex >= 0 ? ReadAttribute(uvIndex, 2, "TEXCOORD_0") : Array.Empty<float>();
                if (normals.Length != 0 && normals.Length != positions.Length)
                    throw new GlbException($"Mesh {meshIndex}: normals and positions don't match.");
                if (uvs.Length != 0 && uvs.Length / 2 != vertexCount)
                    throw new GlbException($"Mesh {meshIndex}: UVs and positions don't match.");

                var indices = ReadIndices(prim["indices"], vertexCount, meshIndex);

                var outPositions = new float[positions.Length];
                for (var v = 0; v < vertexCount; v++)
                {
                    var (x, y, z) = world.Point(positions[v * 3], positions[v * 3 + 1], positions[v * 3 + 2]);
                    outPositions[v * 3] = x;
                    outPositions[v * 3 + 1] = y;
                    outPositions[v * 3 + 2] = -z;
                }

                // Normals follow the inverse transpose of the transform, then flip Z like positions do.
                var normalMatrix = world.NormalMatrix();
                var outNormals = new float[normals.Length];
                for (var v = 0; v < normals.Length / 3; v++)
                {
                    var (nx, ny, nz) = normalMatrix.Direction(normals[v * 3], normals[v * 3 + 1], normals[v * 3 + 2]);
                    var length = (float)Math.Sqrt(nx * nx + ny * ny + nz * nz);
                    if (length > 0)
                    {
                        nx /= length;
                        ny /= length;
                        nz /= length;
                    }
                    outNormals[v * 3] = nx;
                    outNormals[v * 3 + 1] = ny;
                    outNormals[v * 3 + 2] = -nz;
                }

                var outUvs = new float[uvs.Length];
                for (var v = 0; v < uvs.Length / 2; v++)
                {
                    outUvs[v * 2] = uvs[v * 2];
                    outUvs[v * 2 + 1] = 1f - uvs[v * 2 + 1];
                }

                // Handedness flips with Z, which reverses winding; a mirroring transform already reversed it in glTF.
                if (world.Determinant() > 0)
                    ReverseTriangles(indices);

                output.Add(new GlbSubmesh
                {
                    Positions = outPositions,
                    Normals = outNormals,
                    Uvs = outUvs,
                    Indices = indices,
                    Material = Int(prim["material"], -1, "primitive material"),
                    Name = name
                });
            }
        }

        private int[] ReadIndices(JsonValue? accessorJson, int vertexCount, int meshIndex)
        {
            int[] indices;
            if (accessorJson == null)
            {
                if (vertexCount % 3 != 0)
                    throw new GlbException($"Mesh {meshIndex}: a triangle list needs a multiple of 3 vertices.");
                _indices += vertexCount;
                indices = new int[vertexCount];
                for (var i = 0; i < vertexCount; i++)
                    indices[i] = i;
            }
            else
            {
                var accessor = Element(_accessors, Index(accessorJson, _accessors.Count, "indices"), "indices");
                var (count, componentType, start, stride, elementSize) = Access(accessor, "indices", 1);
                if (componentType != UnsignedByte && componentType != UnsignedShort && componentType != UnsignedInt)
                    throw new GlbException($"Mesh {meshIndex}: indices must be unsigned integers.");
                if (count % 3 != 0)
                    throw new GlbException($"Mesh {meshIndex}: a triangle list needs a multiple of 3 indices.");
                _indices += count;
                if (_indices > MaxIndices)
                    throw new GlbException($"The model has more than {MaxIndices:N0} indices.");

                indices = new int[count];
                for (var i = 0; i < count; i++)
                {
                    var value = ReadComponent(start + i * stride, componentType, false);
                    if (value < 0 || value >= vertexCount)
                        throw new GlbException($"Mesh {meshIndex}: a face refers to vertex {value}, but there are {vertexCount}.");
                    indices[i] = (int)value;
                }
            }
            return indices;
        }

        private float[] ReadAttribute(int accessorIndex, int components, string what)
        {
            var (count, componentType, start, stride, _) = Access(Element(_accessors, accessorIndex, what), what, components);
            var result = new float[count * components];
            var normalized = Element(_accessors, accessorIndex, what)["normalized"]?.AsBool() ?? false;
            for (var i = 0; i < count; i++)
                for (var c = 0; c < components; c++)
                {
                    var offset = start + i * stride + c * ComponentSize(componentType);
                    result[i * components + c] = (float)ReadComponent(offset, componentType, normalized);
                }
            return result;
        }

        /// <summary>Validates an accessor and returns where its elements are: count, component type, first byte, stride.</summary>
        private (int count, int componentType, int start, int stride, int elementSize) Access(JsonValue accessor, string what, int components)
        {
            if (accessor["sparse"] != null)
                throw new GlbException($"{what}: sparse accessors aren't supported.");
            var count = Int(accessor["count"], -1, $"{what} count");
            if (count <= 0)
                throw new GlbException($"{what}: no data.");
            var componentType = Int(accessor["componentType"], 0, $"{what} componentType");
            var size = ComponentSize(componentType);
            var type = accessor["type"]?.AsString();
            var expected = type switch { "SCALAR" => 1, "VEC2" => 2, "VEC3" => 3, "VEC4" => 4, _ => -1 };
            if (expected < 0 || expected < components)
                throw new GlbException($"{what}: unexpected data type '{type}'.");

            var viewIndex = Int(accessor["bufferView"], -1, $"{what} bufferView");
            if (viewIndex < 0)
                throw new GlbException($"{what}: no buffer data.");
            var (viewStart, viewLength) = View(viewIndex, what);
            var elementBytes = size * components;
            var stride = viewStride(viewIndex, elementBytes, what);
            var start = viewStart + Int(accessor["byteOffset"], 0, $"{what} byteOffset");
            var end = start + (count - 1) * stride + elementBytes;
            if (end > viewStart + viewLength || start < viewStart)
                throw new GlbException($"{what}: its data runs past the end of the buffer.");
            return (count, componentType, start, stride, size);
        }

        private int viewStride(int viewIndex, int elementBytes, string what)
        {
            var view = Element(_bufferViews, viewIndex, "buffer view");
            var stride = Int(view["byteStride"], elementBytes, $"{what} byteStride");
            if (stride < elementBytes || stride > 255 * 4)
                throw new GlbException($"{what}: the stride is invalid.");
            return stride;
        }

        private (int start, int length) View(int viewIndex, string what)
        {
            var view = Element(_bufferViews, viewIndex, "buffer view");
            var bufferIndex = Int(view["buffer"], 0, "buffer view buffer");
            if (bufferIndex != 0 || Arr(_doc["buffers"]).Count == 0)
                throw new GlbException($"{what}: its data is in a buffer the file doesn't have.");
            var start = Int(view["byteOffset"], 0, "buffer view byteOffset");
            var length = Int(view["byteLength"], -1, "buffer view byteLength");
            if (length < 0 || start < 0 || (long)start + length > _bin.Length)
                throw new GlbException($"{what}: its data runs past the end of the file.");
            return (start, length);
        }

        private double ReadComponent(int offset, int componentType, bool normalized)
        {
            switch (componentType)
            {
                case 5126:
                    var f = BitConverter.ToSingle(_bin, offset);
                    if (float.IsNaN(f) || float.IsInfinity(f))
                        throw new GlbException("The model has a damaged value (not a number).");
                    return f;
                case 5120:
                    return normalized ? Math.Max((sbyte)_bin[offset] / 127.0, -1) : (sbyte)_bin[offset];
                case UnsignedByte:
                    return normalized ? _bin[offset] / 255.0 : _bin[offset];
                case 5122:
                    var s = BitConverter.ToInt16(_bin, offset);
                    return normalized ? Math.Max(s / 32767.0, -1) : s;
                case UnsignedShort:
                    var us = BitConverter.ToUInt16(_bin, offset);
                    return normalized ? us / 65535.0 : us;
                case UnsignedInt:
                    return BitConverter.ToUInt32(_bin, offset);
                default:
                    throw new GlbException($"Unsupported number type {componentType}.");
            }
        }

        private static int ComponentSize(int componentType) => componentType switch
        {
            5120 or UnsignedByte => 1,
            5122 or UnsignedShort => 2,
            5125 or 5126 => 4,
            _ => throw new GlbException($"Unsupported number type {componentType}.")
        };

        private static void ReverseTriangles(int[] indices)
        {
            for (var i = 0; i + 2 < indices.Length; i += 3)
                (indices[i + 1], indices[i + 2]) = (indices[i + 2], indices[i + 1]);
        }

        private void AddOnce(string warning)
        {
            if (!_warnings.Contains(warning))
                _warnings.Add(warning);
        }

        private static IReadOnlyList<JsonValue> Arr(JsonValue? v) => v is { IsArray: true } ? v.Items : Array.Empty<JsonValue>();

        private static JsonValue Element(IReadOnlyList<JsonValue> list, int index, string what)
        {
            if (index < 0 || index >= list.Count)
                throw new GlbException($"The model refers to a {what} that doesn't exist.");
            return list[index];
        }

        private static int Index(JsonValue v, int count, string what)
        {
            var i = Int(v, -1, what);
            if (i < 0 || i >= count)
                throw new GlbException($"The model refers to a {what} that doesn't exist.");
            return i;
        }

        private static int Int(JsonValue? v, int fallback, string what)
        {
            if (v == null || v.IsNull)
                return fallback;
            var n = v.AsNumber();
            if (n == null || n.Value != Math.Floor(n.Value) || Math.Abs(n.Value) > int.MaxValue)
                throw new GlbException($"The model has a bad number for {what}.");
            return (int)n.Value;
        }

        private static double Number(JsonValue? v, string what)
        {
            var n = v?.AsNumber();
            if (n == null || double.IsNaN(n.Value) || double.IsInfinity(n.Value))
                throw new GlbException($"The model has a bad number for {what}.");
            return n.Value;
        }

        private static float Clamp01(double v) => (float)Math.Max(0, Math.Min(1, v));

        private static float[] Vec(JsonValue? v, int count, float[] fallback, string what)
        {
            var items = Arr(v);
            if (items.Count == 0)
                return fallback;
            if (items.Count != count)
                throw new GlbException($"The model has a bad {what}.");
            var result = new float[count];
            for (var i = 0; i < count; i++)
                result[i] = (float)Number(items[i], what);
            return result;
        }
    }
}
