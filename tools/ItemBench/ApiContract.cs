// 0.15.0 (C15-01): the public API, frozen. Other mods call YazsCompanion.Api.Extensions through REFLECTION, by name and
// parameter types (README "Extensions"), so a renamed call or a changed parameter breaks them without a compile error anywhere.
// This check reads the BUILT DLL's metadata (System.Reflection.Metadata: nothing is loaded, no game assembly needed) and
// compares every public type and member of the namespace YazsCompanion.Api, and the number ApiVersion returns, with
// api_v<ApiVersion>.txt next to this file:
//   - a line of the file missing from the DLL: a call that changed or went away - it breaks the mods that use it;
//   - a public member the file does not have: a new call - write api_v<N+1>.txt and raise ApiVersion with it;
//   - ApiVersion without its api_v<N>.txt: the same.
// Without a built DLL (a fresh clone, the GitHub workflow) the check is skipped; --strict (the release gate) fails it instead.
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace YazsCompanion.Bench
{
    static class ApiContract
    {
        const string Namespace = "YazsCompanion.Api";

        static string ModDir { get { return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "YazsCompanion.Mod")); } }
        public static string DefaultDll { get { return Path.Combine(ModDir, "bin", "Release", "YazsCompanionMod.dll"); } }
        static string BenchDir { get { return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..")); } }

        static int _bad;
        static void Check(string id, string what, bool ok, string detail = null)
        {
            if (!ok) _bad++;
            Console.WriteLine("  " + (ok ? "ok  " : "BAD ") + " " + id + " " + what + (string.IsNullOrEmpty(detail) ? "" : ": " + detail));
        }

        public static int Run(bool strict)
        {
            _bad = 0;
            Console.WriteLine("\n=== 0.15.0: the public API frozen - " + Namespace + " in the built DLL against api_v<ApiVersion>.txt (C15-01)");
            string dll = DefaultDll;
            if (!File.Exists(dll))
            {
                if (strict) Check("A0", "the built DLL is there (--strict)", false, dll);
                else Console.WriteLine("  skipped: no built DLL at " + dll + " (build the mod first; --strict makes a missing DLL a failure)");
                Console.WriteLine("  " + (_bad == 0 ? "all as wanted" : _bad + " BAD"));
                return _bad;
            }
            int version; List<string> surface;
            try { surface = Surface(dll, out version); }
            catch (Exception e)
            {
                Check("A0", "the built DLL's metadata is readable", false, e.GetType().Name + ": " + e.Message);
                Console.WriteLine("  " + _bad + " BAD");
                return _bad;
            }
            var newest = Directory.GetFiles(ModDir, "*.cs", SearchOption.AllDirectories).Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
                .Select(f => File.GetLastWriteTimeUtc(f)).DefaultIfEmpty(DateTime.MinValue).Max();
            if (File.GetLastWriteTimeUtc(dll) < newest) Console.WriteLine("  (the DLL is older than the newest source file: what follows is the last build's API)");

            string file = Path.Combine(BenchDir, "api_v" + version + ".txt");
            Check("A1", "ApiVersion returns a number, and api_v" + version + ".txt is there", version > 0 && File.Exists(file), version > 0 ? file : "ApiVersion not read from the IL");
            if (version > 0 && File.Exists(file))
            {
                var frozen = File.ReadAllLines(file).Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith("#")).ToList();
                var missing = frozen.Where(l => !surface.Contains(l)).ToList();
                var added = surface.Where(l => !frozen.Contains(l)).ToList();
                Check("A2", "every frozen line of api_v" + version + ".txt is in the DLL (" + frozen.Count + " lines) - a missing one breaks the mods that call it",
                    missing.Count == 0, missing.Count == 0 ? null : string.Join(" | ", missing));
                Check("A3", "no public member the file lacks - a new call takes api_v" + (version + 1) + ".txt and ApiVersion " + (version + 1),
                    added.Count == 0, added.Count == 0 ? null : string.Join(" | ", added));
                var types = surface.Where(l => l.StartsWith("type ")).ToList();
                var members = surface.Where(l => !l.StartsWith("type ")).ToList();
                var instance = members.Where(l => !l.Contains(" static ")).ToList();
                // a member's own qualified name left out, no type of this mod may remain (a caller without a reference has none)
                var ours = members.Where(l => types.Aggregate(l, (acc, t) => acc.Replace(t.Substring(t.LastIndexOf(' ') + 1) + ".", "")).Contains("YazsCompanion.")).ToList();
                Check("A4", "the surface is one public class, every member static, BCL types only (callable by reflection without a reference)",
                    types.Count == 1 && instance.Count == 0 && ours.Count == 0,
                    string.Join(" | ", types.Concat(instance.Select(l => "not static: " + l)).Concat(ours.Select(l => "a type of this mod: " + l))));
            }
            Console.WriteLine("  " + (_bad == 0 ? "all as wanted" : _bad + " BAD"));
            return _bad;
        }

        /// <summary>ItemBench --api-surface: the built DLL's surface in the file's format (the start of an api_v&lt;N&gt;.txt).</summary>
        public static int Print()
        {
            string dll = DefaultDll;
            if (!File.Exists(dll)) { Console.WriteLine("no built DLL at " + dll + " - build the mod first"); return 2; }
            int version;
            var lines = Surface(dll, out version);
            Console.WriteLine("# The public surface of " + Namespace + " at ApiVersion " + version + " (" + Path.GetFileName(dll) + "), frozen: the bench");
            Console.WriteLine("# (ApiContract.cs) compares the built DLL with it. Other mods call these through reflection, by name and parameter");
            Console.WriteLine("# types: a line changes only with a new ApiVersion and a new api_v<N>.txt, never in place.");
            foreach (var l in lines) Console.WriteLine(l);
            return 0;
        }

        /// <summary>Every public type and member of <see cref="Namespace"/> in <paramref name="dll"/>, one sorted line each, and the
        /// number ApiVersion's getter returns (-1: not read).</summary>
        public static List<string> Surface(string dll, out int apiVersion)
        {
            apiVersion = -1;
            var lines = new List<string>();
            using (var fs = new FileStream(dll, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var pe = new PEReader(fs))
            {
                var md = pe.GetMetadataReader();
                var names = new Names(md);
                foreach (var th in md.TypeDefinitions)
                {
                    var td = md.GetTypeDefinition(th);
                    if (md.GetString(td.Namespace) != Namespace || (td.Attributes & TypeAttributes.VisibilityMask) != TypeAttributes.Public) continue;
                    string type = Namespace + "." + md.GetString(td.Name);
                    bool isStatic = (td.Attributes & TypeAttributes.Abstract) != 0 && (td.Attributes & TypeAttributes.Sealed) != 0;
                    string kind = (td.Attributes & TypeAttributes.Interface) != 0 ? "interface" : isStatic ? "static class" : "class";
                    lines.Add("type public " + kind + " " + type);
                    var accessors = new HashSet<MethodDefinitionHandle>();
                    foreach (var ph in td.GetProperties())
                    {
                        var p = md.GetPropertyDefinition(ph);
                        var acc = p.GetAccessors();
                        var parts = new List<string>(); string mods = null; string ptype = null;
                        foreach (var a in new[] { acc.Getter, acc.Setter })
                        {
                            if (a.IsNil) continue;
                            accessors.Add(a);
                            var m = md.GetMethodDefinition(a);
                            if ((m.Attributes & MethodAttributes.MemberAccessMask) != MethodAttributes.Public) continue;
                            mods = "public" + ((m.Attributes & MethodAttributes.Static) != 0 ? " static" : "");
                            parts.Add(a == acc.Getter ? "get;" : "set;");
                            var sig = m.DecodeSignature(names, null);
                            ptype = a == acc.Getter ? sig.ReturnType : sig.ParameterTypes.Last();
                            if (a == acc.Getter && md.GetString(p.Name) == "ApiVersion") apiVersion = Constant(pe, m);
                        }
                        if (mods != null) lines.Add("property " + mods + " " + ptype + " " + type + "." + md.GetString(p.Name) + " { " + string.Join(" ", parts) + " }");
                    }
                    foreach (var mh in td.GetMethods())
                    {
                        if (accessors.Contains(mh)) continue;
                        var m = md.GetMethodDefinition(mh);
                        if ((m.Attributes & MethodAttributes.MemberAccessMask) != MethodAttributes.Public) continue;
                        var sig = m.DecodeSignature(names, null);
                        var pnames = new Dictionary<int, string>();
                        foreach (var prh in m.GetParameters()) { var pr = md.GetParameter(prh); if (pr.SequenceNumber > 0) pnames[pr.SequenceNumber] = md.GetString(pr.Name); }
                        var ps = new List<string>();
                        for (int i = 0; i < sig.ParameterTypes.Length; i++) { string n; ps.Add(sig.ParameterTypes[i] + (pnames.TryGetValue(i + 1, out n) ? " " + n : "")); }
                        string name = md.GetString(m.Name);
                        lines.Add("method public" + ((m.Attributes & MethodAttributes.Static) != 0 ? " static" : "") + " " + (name == ".ctor" ? "" : sig.ReturnType + " ") + type + "." + name + "(" + string.Join(", ", ps) + ")");
                    }
                    foreach (var fh in td.GetFields())
                    {
                        var f = md.GetFieldDefinition(fh);
                        if ((f.Attributes & FieldAttributes.FieldAccessMask) != FieldAttributes.Public) continue;
                        lines.Add("field public" + ((f.Attributes & FieldAttributes.Static) != 0 ? " static" : "") + " " + f.DecodeSignature(names, null) + " " + type + "." + md.GetString(f.Name));
                    }
                    foreach (var eh in td.GetEvents())
                    {
                        var ev = md.GetEventDefinition(eh);
                        var add = ev.GetAccessors().Adder;
                        if (add.IsNil || (md.GetMethodDefinition(add).Attributes & MethodAttributes.MemberAccessMask) != MethodAttributes.Public) continue;
                        lines.Add("event public " + type + "." + md.GetString(ev.Name));
                    }
                }
            }
            lines.Sort(StringComparer.Ordinal);
            return lines;
        }

        /// <summary>The int a getter's body returns when it is 'ldc.i4.x; ret' (an expression-bodied constant); -1 otherwise.</summary>
        static int Constant(PEReader pe, MethodDefinition m)
        {
            if (m.RelativeVirtualAddress == 0) return -1;
            var il = pe.GetMethodBody(m.RelativeVirtualAddress).GetILBytes();
            if (il == null || il.Length < 2) return -1;
            int op = il[0];
            if (op >= 0x16 && op <= 0x1E && il[1] == 0x2A) return op - 0x16;                        // ldc.i4.0 .. ldc.i4.8; ret
            if (op == 0x1F && il.Length >= 3 && il[2] == 0x2A) return (sbyte)il[1];                  // ldc.i4.s n; ret
            if (op == 0x20 && il.Length >= 6 && il[5] == 0x2A) return BitConverter.ToInt32(il, 1);   // ldc.i4 n; ret
            return -1;
        }

        /// <summary>Type names as C# writes them: the keyword for a primitive, Namespace.Name otherwise, generics with &lt;&gt;.</summary>
        sealed class Names : ISignatureTypeProvider<string, object>
        {
            readonly MetadataReader _md;
            public Names(MetadataReader md) { _md = md; }

            public string GetPrimitiveType(PrimitiveTypeCode t)
            {
                switch (t)
                {
                    case PrimitiveTypeCode.Void: return "void";
                    case PrimitiveTypeCode.Boolean: return "bool";
                    case PrimitiveTypeCode.Char: return "char";
                    case PrimitiveTypeCode.SByte: return "sbyte";
                    case PrimitiveTypeCode.Byte: return "byte";
                    case PrimitiveTypeCode.Int16: return "short";
                    case PrimitiveTypeCode.UInt16: return "ushort";
                    case PrimitiveTypeCode.Int32: return "int";
                    case PrimitiveTypeCode.UInt32: return "uint";
                    case PrimitiveTypeCode.Int64: return "long";
                    case PrimitiveTypeCode.UInt64: return "ulong";
                    case PrimitiveTypeCode.Single: return "float";
                    case PrimitiveTypeCode.Double: return "double";
                    case PrimitiveTypeCode.String: return "string";
                    case PrimitiveTypeCode.Object: return "object";
                    case PrimitiveTypeCode.IntPtr: return "System.IntPtr";
                    case PrimitiveTypeCode.UIntPtr: return "System.UIntPtr";
                    default: return "System." + t;
                }
            }

            public string GetTypeFromDefinition(MetadataReader reader, TypeDefinitionHandle handle, byte rawTypeKind)
            {
                var td = reader.GetTypeDefinition(handle);
                var outer = td.GetDeclaringType();
                return outer.IsNil ? Join(reader.GetString(td.Namespace), reader.GetString(td.Name)) : GetTypeFromDefinition(reader, outer, 0) + "+" + reader.GetString(td.Name);
            }

            public string GetTypeFromReference(MetadataReader reader, TypeReferenceHandle handle, byte rawTypeKind)
            {
                var tr = reader.GetTypeReference(handle);
                if (tr.ResolutionScope.Kind == HandleKind.TypeReference) return GetTypeFromReference(reader, (TypeReferenceHandle)tr.ResolutionScope, 0) + "+" + reader.GetString(tr.Name);
                return Join(reader.GetString(tr.Namespace), reader.GetString(tr.Name));
            }

            static string Join(string ns, string name) { return ns.Length > 0 ? ns + "." + name : name; }

            public string GetTypeFromSpecification(MetadataReader reader, object genericContext, TypeSpecificationHandle handle, byte rawTypeKind)
            {
                return reader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);
            }

            public string GetSZArrayType(string elementType) { return elementType + "[]"; }
            public string GetArrayType(string elementType, ArrayShape shape) { return elementType + "[" + new string(',', Math.Max(0, shape.Rank - 1)) + "]"; }
            public string GetByReferenceType(string elementType) { return "ref " + elementType; }
            public string GetPointerType(string elementType) { return elementType + "*"; }
            public string GetPinnedType(string elementType) { return elementType; }
            public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired) { return unmodifiedType; }
            public string GetFunctionPointerType(MethodSignature<string> signature) { return "delegate*<" + string.Join(", ", signature.ParameterTypes.Concat(new[] { signature.ReturnType })) + ">"; }
            public string GetGenericMethodParameter(object genericContext, int index) { return "!!" + index; }
            public string GetGenericTypeParameter(object genericContext, int index) { return "!" + index; }

            public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments)
            {
                int tick = genericType.IndexOf('`');
                return (tick >= 0 ? genericType.Substring(0, tick) : genericType) + "<" + string.Join(", ", typeArguments) + ">";
            }
        }
    }
}
