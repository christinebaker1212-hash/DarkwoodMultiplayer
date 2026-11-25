// StubGen: builds compile-time reference stubs for assemblies we don't have
// (the game's Assembly-CSharp, Rewired_Core) from exactly the type/member
// references the original mod DLL makes. The stubs are never shipped - at
// runtime the game's real assemblies are bound by name/signature.
//
// usage: StubGen <original-mod.dll> <out-dir> <base-types.txt> <unity-ref-dir>
//   base-types.txt: lines "Full.Type.Name = UnityEngine.MonoBehaviour" (or another stub type)
using Mono.Cecil;
using Mono.Cecil.Cil;

var src = ModuleDefinition.ReadModule(args[0]);
var outDir = args[1];
var baseMap = File.Exists(args[2])
    ? File.ReadAllLines(args[2]).Where(l => l.Contains('=') && !l.TrimStart().StartsWith("#"))
        .ToDictionary(l => l.Split('=')[0].Trim(), l => l.Split('=')[1].Trim())
    : new Dictionary<string, string>();
var resolver = new DefaultAssemblyResolver();
resolver.AddSearchDirectory(args[3]);
Directory.CreateDirectory(outDir);

string[] stubScopes = { "Assembly-CSharp", "Rewired_Core", "com.rlabrecque.steamworks.net" };
bool InScope(TypeReference t) => stubScopes.Contains(t.Scope?.Name);

// --- force-load all signatures so Cecil learns which typerefs are value types
var staticFields = new HashSet<string>();
var instanceFields = new HashSet<string>();
foreach (var t in src.GetTypes())
foreach (var m in t.Methods)
{
    if (!m.HasBody) continue;
    foreach (var ins in m.Body.Instructions)
    {
        if (ins.Operand is FieldReference fr && InScope(Root(fr.DeclaringType)))
        {
            var key = FieldKey(fr);
            if (ins.OpCode.Code is Code.Ldsfld or Code.Stsfld or Code.Ldsflda) staticFields.Add(key);
            else instanceFields.Add(key);
        }
    }
}
_ = src.GetMemberReferences().ToList();

static TypeReference Root(TypeReference t) { while (t is TypeSpecification ts) t = ts.ElementType; return t; }
static string FieldKey(FieldReference f) => Root(f.DeclaringType).FullName + "::" + f.Name;

// value-type detection: Cecil marks typerefs seen as `valuetype` in signatures
var valueTypes = new HashSet<string>();
void ScanType(TypeReference? t)
{
    if (t == null) return;
    if (t is GenericInstanceType git) { ScanType(git.ElementType); foreach (var a in git.GenericArguments) ScanType(a); return; }
    if (t is TypeSpecification ts) { ScanType(ts.ElementType); return; }
    if (InScope(t) && t.IsValueType) valueTypes.Add(t.FullName);
}
foreach (var mr in src.GetMemberReferences())
{
    ScanType(mr.DeclaringType);
    if (mr is MethodReference m) { ScanType(m.ReturnType); foreach (var p in m.Parameters) ScanType(p.ParameterType); }
    if (mr is FieldReference f) ScanType(f.FieldType);
}
foreach (var t in src.GetTypes())
{
    foreach (var f in t.Fields) ScanType(f.FieldType);
    foreach (var m in t.Methods)
    {
        ScanType(m.ReturnType); foreach (var p in m.Parameters) ScanType(p.ParameterType);
        if (m.HasBody) { foreach (var v in m.Body.Variables) ScanType(v.VariableType);
            foreach (var ins in m.Body.Instructions) if (ins.Operand is TypeReference tr) ScanType(tr); }
    }
}

// --- build one stub module per scope
foreach (var scope in stubScopes)
{
    var asmRef = src.AssemblyReferences.FirstOrDefault(a => a.Name == scope);
    if (asmRef == null) continue;
    var asm = AssemblyDefinition.CreateAssembly(new AssemblyNameDefinition(scope, asmRef.Version), scope, new ModuleParameters
    { Kind = ModuleKind.Dll, AssemblyResolver = resolver, Runtime = TargetRuntime.Net_4_0 });
    var mod = asm.MainModule;
    var map = new Dictionary<string, TypeDefinition>();
    var msCorlib = src.TypeSystem;
    var deferredInvoke = new List<MethodDefinition>();
    var seenSigs = new HashSet<string>();

    TypeDefinition GetDef(TypeReference r)
    {
        r = Root(r);
        if (map.TryGetValue(r.FullName, out var d)) return d;
        bool vt = valueTypes.Contains(r.FullName);
        var attrs = TypeAttributes.Public | TypeAttributes.BeforeFieldInit;
        if (r.IsNested) attrs = TypeAttributes.NestedPublic | TypeAttributes.BeforeFieldInit;
        d = new TypeDefinition(r.IsNested ? "" : r.Namespace, r.Name, attrs);
        map[r.FullName] = d; // register before recursing
        int gp = r.HasGenericParameters ? r.GenericParameters.Count : (r.Name.Contains('`') ? int.Parse(r.Name.Split('`')[1]) : 0);
        if (r.IsNested) gp = GetDef(r.DeclaringType).GenericParameters.Count + (r.Name.Contains('`') ? int.Parse(r.Name.Split('`')[1]) : 0);
        for (int i = 0; i < gp; i++) d.GenericParameters.Add(new GenericParameter("T" + i, d));
        if (vt)
        {
            // value types: enum unless members are referenced (then struct)
            bool hasMembers = src.GetMemberReferences().Any(m => Root(m.DeclaringType).FullName == r.FullName)
                              || baseMap.GetValueOrDefault(r.FullName) == "struct";
            if (hasMembers) { d.BaseType = new TypeReference("System", "ValueType", mod, mod.TypeSystem.CoreLibrary); d.Attributes |= TypeAttributes.Sealed | TypeAttributes.SequentialLayout; }
            else
            {
                d.BaseType = new TypeReference("System", "Enum", mod, mod.TypeSystem.CoreLibrary); d.Attributes |= TypeAttributes.Sealed;
                d.Fields.Add(new FieldDefinition("value__", FieldAttributes.Public | FieldAttributes.SpecialName | FieldAttributes.RTSpecialName, mod.TypeSystem.Int32));
            }
        }
        else if (baseMap.TryGetValue(r.FullName, out var b))
        {
            if (b == "interface") { d.Attributes |= TypeAttributes.Interface | TypeAttributes.Abstract; d.Attributes &= ~TypeAttributes.BeforeFieldInit; }
            else if (b.StartsWith("delegate"))
            {
                d.BaseType = new TypeReference("System", "MulticastDelegate", mod, mod.TypeSystem.CoreLibrary); d.Attributes |= TypeAttributes.Sealed;
                var ctor = new MethodDefinition(".ctor", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName, mod.TypeSystem.Void) { ImplAttributes = MethodImplAttributes.Runtime };
                ctor.Parameters.Add(new ParameterDefinition("o", ParameterAttributes.None, mod.TypeSystem.Object));
                ctor.Parameters.Add(new ParameterDefinition("m", ParameterAttributes.None, mod.TypeSystem.IntPtr));
                d.Methods.Add(ctor);
                // spec: delegate:ret(T0,...)   T<n> = generic param n, else a type full name
                var spec = b.Contains(':') ? b.Substring(b.IndexOf(':') + 1) : "void()";
                var inv = new MethodDefinition("Invoke", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.Virtual | MethodAttributes.NewSlot, mod.TypeSystem.Void) { ImplAttributes = MethodImplAttributes.Runtime };
                TypeReference P(string n) => n == "void" ? mod.TypeSystem.Void : n.StartsWith("T") && int.TryParse(n.Substring(1), out var gi) ? d.GenericParameters[gi] : ResolveBase(n);
                inv.ReturnType = P(spec.Substring(0, spec.IndexOf('(')));
                foreach (var a in spec.Substring(spec.IndexOf('(') + 1).TrimEnd(')').Split(',', StringSplitOptions.RemoveEmptyEntries))
                    inv.Parameters.Add(new ParameterDefinition("p", ParameterAttributes.None, P(a.Trim())));
                deferredInvoke.Add(inv); d.Methods.Add(inv);
            }
            else d.BaseType = ResolveBase(b);
        }
        else d.BaseType = mod.TypeSystem.Object;
        if (r.IsNested) GetDef(r.DeclaringType).NestedTypes.Add(d); else mod.Types.Add(d);
        if (!d.IsInterface && !d.IsValueType && d.BaseType?.FullName != "System.MulticastDelegate" && d.BaseType?.FullName != "System.Enum" && d.BaseType?.FullName != "System.ValueType")
        {
            // protected parameterless ctor so plugin subclasses compile
            var ctor = NewMethod(".ctor", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName, mod.TypeSystem.Void);
            d.Methods.Add(ctor);
        }
        return d;
    }

    TypeReference ResolveBase(string name)
    {
        if (map.TryGetValue(name, out var d)) return d;
        var srcRef = src.GetTypeReferences().FirstOrDefault(t => t.FullName == name);
        if (srcRef != null && InScope(srcRef)) return GetDef(srcRef);
        if (srcRef != null) return mod.ImportReference(srcRef);
        // UnityEngine type not referenced directly by the mod: resolve from unity refs
        foreach (var f in Directory.GetFiles(args[3], "UnityEngine*.dll"))
        {
            var m = ModuleDefinition.ReadModule(f);
            var t = m.GetType(name);
            if (t != null) return mod.ImportReference(t);
        }
        throw new Exception("base type not found: " + name);
    }

    MethodDefinition NewMethod(string name, MethodAttributes a, TypeReference ret)
    {
        var md = new MethodDefinition(name, a, ret);
        var il = md.Body.GetILProcessor();
        il.Emit(OpCodes.Ldnull); il.Emit(OpCodes.Throw);
        return md;
    }

    TypeReference Imp(TypeReference t, IGenericParameterProvider? ctx)
    {
        switch (t)
        {
            case GenericParameter gp:
                if (gp.Type == GenericParameterType.Method && ctx is MethodDefinition md2) return md2.GenericParameters[gp.Position];
                if (gp.Type == GenericParameterType.Type && ctx is MethodDefinition md3) return md3.DeclaringType.GenericParameters[gp.Position];
                if (gp.Type == GenericParameterType.Type && ctx is TypeDefinition td) return td.GenericParameters[gp.Position];
                throw new Exception("generic param ctx");
            case ByReferenceType br: return new ByReferenceType(Imp(br.ElementType, ctx));
            case ArrayType at: return new ArrayType(Imp(at.ElementType, ctx), at.Rank);
            case PointerType pt: return new PointerType(Imp(pt.ElementType, ctx));
            case GenericInstanceType gi:
                var g = new GenericInstanceType(Imp(gi.ElementType, ctx));
                foreach (var a in gi.GenericArguments) g.GenericArguments.Add(Imp(a, ctx));
                return g;
            case RequiredModifierType rm: return Imp(rm.ElementType, ctx);
            case OptionalModifierType om: return Imp(om.ElementType, ctx);
        }
        if (scope == t.Scope?.Name) return GetDef(t);
        if (InScope(t))
        {
            // cross-stub reference (e.g. Assembly-CSharp -> Rewired_Core): plain typeref
            var anr = mod.AssemblyReferences.FirstOrDefault(a => a.Name == t.Scope.Name);
            if (anr == null) { anr = new AssemblyNameReference(t.Scope.Name, ((AssemblyNameReference)t.Scope).Version); mod.AssemblyReferences.Add(anr); }
            var tr = new TypeReference(t.Namespace, t.Name, mod, anr, valueTypes.Contains(t.FullName));
            if (t.IsNested) tr.DeclaringType = Imp(t.DeclaringType, ctx);
            return tr;
        }
        return mod.ImportReference(t);
    }

    // every in-scope type referenced anywhere
    foreach (var t in src.GetTypeReferences().Where(t => t.Scope?.Name == scope)) GetDef(t);
    foreach (var name in baseMap.Keys.Where(k => baseMap[k] != "struct")) { }

    // members
    foreach (var mr in src.GetMemberReferences())
    {
        var root = Root(mr.DeclaringType);
        if (root.Scope?.Name != scope) continue;
        var d = GetDef(root);
        if (mr is FieldReference fr)
        {
            if (d.Fields.Any(f => f.Name == fr.Name)) continue;
            var fa = FieldAttributes.Public;
            if (staticFields.Contains(FieldKey(fr)) && !instanceFields.Contains(FieldKey(fr))) fa |= FieldAttributes.Static;
            d.Fields.Add(new FieldDefinition(fr.Name, fa, Imp(fr.FieldType, d)));
        }
        else if (mr is MethodReference m)
        {
            var em = m is GenericInstanceMethod gim ? gim.ElementMethod : m;
            var sigKey = d.FullName + "::" + em.Name + "`" + em.GenericParameters.Count + "(" + string.Join(",", em.Parameters.Select(p => p.ParameterType.FullName)) + ")" + em.HasThis;
            if (!seenSigs.Add(sigKey))
                continue;
            if (em.Name == ".ctor" && em.Parameters.Count == 0) continue; // added already
            if (d.BaseType?.FullName == "System.MulticastDelegate" && (em.Name == ".ctor" || em.Name == "Invoke")) continue;
            var ma = MethodAttributes.Public | MethodAttributes.HideBySig;
            if (!em.HasThis) ma |= MethodAttributes.Static;
            bool special = em.Name.StartsWith("get_") || em.Name.StartsWith("set_") || em.Name.StartsWith("add_") || em.Name.StartsWith("remove_") || em.Name.StartsWith("op_") || em.Name == ".ctor";
            if (special) ma |= MethodAttributes.SpecialName;
            if (em.Name == ".ctor") ma |= MethodAttributes.RTSpecialName;
            if (d.IsInterface) ma |= MethodAttributes.Abstract | MethodAttributes.Virtual | MethodAttributes.NewSlot;
            if ((baseMap.GetValueOrDefault(root.FullName) ?? "").StartsWith("delegate") && (em.Name == "Invoke" || em.Name == "BeginInvoke" || em.Name == "EndInvoke"))
                ma |= MethodAttributes.Virtual | MethodAttributes.NewSlot;
            var md = new MethodDefinition(em.Name, ma, mod.TypeSystem.Void);
            foreach (var g in em.GenericParameters) md.GenericParameters.Add(new GenericParameter(g.Name, md));
            d.Methods.Add(md);
            md.ReturnType = Imp(em.ReturnType, md);
            int i = 0;
            foreach (var p in em.Parameters)
            {
                var pt = Imp(p.ParameterType, md);
                var pd = new ParameterDefinition("p" + i++, ParameterAttributes.None, pt);
                if (p.ParameterType is ByReferenceType && p.IsOut) pd.IsOut = true;
                md.Parameters.Add(pd);
            }
            if (!d.IsInterface && !((baseMap.GetValueOrDefault(root.FullName) ?? "").StartsWith("delegate") && (ma & MethodAttributes.Virtual) != 0))
            { var il = md.Body.GetILProcessor(); il.Emit(OpCodes.Ldnull); il.Emit(OpCodes.Throw); }
            else md.ImplAttributes = (baseMap.GetValueOrDefault(root.FullName) ?? "").StartsWith("delegate") ? MethodImplAttributes.Runtime : 0;
        }
    }

    // properties / events for accessor methods
    foreach (var d in map.Values)
    {
        foreach (var g in d.Methods.Where(m => m.Name.StartsWith("get_") || m.Name.StartsWith("set_")).ToList())
        {
            var pname = g.Name.Substring(4);
            var isGet = g.Name.StartsWith("get_");
            var ptype = isGet ? g.ReturnType : g.Parameters.Last().ParameterType;
            var prop = d.Properties.FirstOrDefault(p => p.Name == pname);
            if (prop == null) { prop = new PropertyDefinition(pname, PropertyAttributes.None, ptype); d.Properties.Add(prop); }
            if (isGet) prop.GetMethod = g; else prop.SetMethod = g;
            if (pname == "Item" && !d.CustomAttributes.Any())
            {
                var dma = new TypeReference("System.Reflection", "DefaultMemberAttribute", mod, mod.TypeSystem.CoreLibrary);
                var ctor = new MethodReference(".ctor", mod.TypeSystem.Void, dma) { HasThis = true };
                ctor.Parameters.Add(new ParameterDefinition(mod.TypeSystem.String));
                var ca = new CustomAttribute(ctor); ca.ConstructorArguments.Add(new CustomAttributeArgument(mod.TypeSystem.String, "Item"));
                d.CustomAttributes.Add(ca);
            }
        }
        foreach (var a in d.Methods.Where(m => m.Name.StartsWith("add_")).ToList())
        {
            var ename = a.Name.Substring(4);
            var ev = new EventDefinition(ename, EventAttributes.None, a.Parameters[0].ParameterType) { AddMethod = a };
            ev.RemoveMethod = d.Methods.FirstOrDefault(m => m.Name == "remove_" + ename);
            if (ev.RemoveMethod == null)
            {
                var r = NewMethod("remove_" + ename, a.Attributes, mod.TypeSystem.Void);
                r.Parameters.Add(new ParameterDefinition("p0", ParameterAttributes.None, a.Parameters[0].ParameterType));
                d.Methods.Add(r); ev.RemoveMethod = r;
            }
            d.Events.Add(ev);
        }
    }

    // virtual methods overridden by the mod's own subclasses
    foreach (var t in src.GetTypes().Where(t => t.BaseType != null && Root(t.BaseType).Scope?.Name == scope))
    {
        var d = GetDef(t.BaseType);
        foreach (var m in t.Methods.Where(m => m.IsVirtual && !m.IsNewSlot))
        {
            var existing = d.Methods.FirstOrDefault(x => x.Name == m.Name && x.Parameters.Count == m.Parameters.Count);
            if (existing == null)
            {
                existing = NewMethod(m.Name, MethodAttributes.Public | MethodAttributes.HideBySig, Imp(m.ReturnType, d));
                foreach (var p in m.Parameters) existing.Parameters.Add(new ParameterDefinition(p.Name, ParameterAttributes.None, Imp(p.ParameterType, d)));
                d.Methods.Add(existing);
            }
            existing.Attributes |= MethodAttributes.Virtual | MethodAttributes.NewSlot;
            existing.Attributes = (existing.Attributes & ~MethodAttributes.MemberAccessMask) | (m.Attributes & MethodAttributes.MemberAccessMask);
        }
    }

    asm.Write(Path.Combine(outDir, scope + ".dll"));
    Console.WriteLine($"{scope}: {map.Count} types, {map.Values.Sum(t => t.Methods.Count)} methods, {map.Values.Sum(t => t.Fields.Count)} fields");
}

static string SigName(TypeReference t) => t.FullName;
