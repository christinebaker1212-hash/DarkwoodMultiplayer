// RefCheck: compares the external (game / Unity / Steam) API surface of two builds of the mod.
// usage: RefCheck <original.dll> <new.dll> [--per-method]
// Prints: assembly refs of the new build, external members the new build uses that the original never did
// (each must exist in the real game: review by hand), and with --per-method the methods whose external calls differ.
using Mono.Cecil; using Mono.Cecil.Cil;
var a = ModuleDefinition.ReadModule(args[0]); var b = ModuleDefinition.ReadModule(args[1]);
bool perMethod = args.Contains("--per-method");
string[] mine = { "DarkwoodCoopOnline" };
bool External(MemberReference r) { var t = r.DeclaringType; while (t is TypeSpecification ts) t = ts.ElementType; return t != null && !mine.Contains(t.Scope?.Name) && t.Module == r.Module && t.Scope is AssemblyNameReference; }
string Key(MemberReference r) => r.FullName.Replace("DarkwoodCoopOnline.", "");
HashSet<string> All(ModuleDefinition m) => m.GetMemberReferences().Where(External).Select(Key).ToHashSet();
Dictionary<string, HashSet<string>> PerMethod(ModuleDefinition m) {
  var d = new Dictionary<string, HashSet<string>>();
  foreach (var t in m.GetTypes()) foreach (var me in t.Methods.Where(x => x.HasBody)) {
    var name = (t.FullName + "::" + me.Name + "(" + me.Parameters.Count + ")").Replace("/", ".");
    // compiler-generated closures/lambdas get different names: fold them into their declaring type
    if (name.Contains('<')) name = name.Substring(0, name.IndexOf('<')) + "<generated>";
    if (!d.TryGetValue(name, out var s)) d[name] = s = new();
    foreach (var i in me.Body.Instructions) if (i.Operand is MemberReference r && r is not TypeReference && r.DeclaringType != null) {
      var t2 = r.DeclaringType; while (t2 is TypeSpecification ts) t2 = ts.ElementType;
      if (t2.Scope is AssemblyNameReference an && an.Name != "mscorlib" && an.Name != "System" && an.Name != "System.Core") s.Add(Key(r));
    }
  }
  return d;
}
Console.WriteLine("== assembly refs (new build)");
foreach (var r in b.AssemblyReferences) Console.WriteLine($"  {r.FullName}");
var na = All(a); var nb = All(b);
var added = nb.Except(na).Where(x => !x.Contains("mscorlib") ).OrderBy(x => x).ToList();
Console.WriteLine($"== external members only in new build: {added.Count}");
foreach (var x in added) Console.WriteLine("  + " + x);
var removed = na.Except(nb).OrderBy(x => x).ToList();
Console.WriteLine($"== external members only in original: {removed.Count}");
foreach (var x in removed) Console.WriteLine("  - " + x);
if (perMethod) {
  var pa = PerMethod(a); var pb = PerMethod(b); int diffs = 0;
  foreach (var k in pa.Keys.Union(pb.Keys).OrderBy(x => x)) {
    var sa = pa.GetValueOrDefault(k) ?? new(); var sb = pb.GetValueOrDefault(k) ?? new();
    if (sa.SetEquals(sb)) continue; diffs++;
    Console.WriteLine($"~ {k}"); foreach (var x in sb.Except(sa)) Console.WriteLine($"    + {x}"); foreach (var x in sa.Except(sb)) Console.WriteLine($"    - {x}");
  }
  Console.WriteLine($"== methods with different game/Unity calls: {diffs}");
}
