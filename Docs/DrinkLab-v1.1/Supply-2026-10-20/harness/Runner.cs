using System; using System.Collections.Generic; using System.Linq; using System.Reflection; using NUnit.Framework;
static class Runner {
  static int Main(string[] a) {
    if (a.Length > 0 && a[0] == "sim") return Sim.Run(a.Skip(1).ToArray());
    int pass=0, fail=0; string filter = a.Length>0? a[0] : null;
    foreach (var t in typeof(Runner).Assembly.GetTypes().Where(x=>x.GetMethods().Any(m=>m.GetCustomAttribute<TestAttribute>()!=null || m.GetCustomAttributes<TestCaseAttribute>().Any()))) {
      if (filter!=null && !t.Name.Contains(filter)) continue;
      foreach (var m in t.GetMethods()) {
        var cases = m.GetCustomAttributes<TestCaseAttribute>().Select(c=>c.Arguments).ToList();
        if (m.GetCustomAttribute<TestAttribute>()!=null) {
          var ps = m.GetParameters();
          if (ps.Length == 0) cases.Add(new object[0]);
          else { var v = ps[0].GetCustomAttribute<ValuesAttribute>(); foreach (var x in (IEnumerable<object>)typeof(ValuesAttribute).GetField("data", BindingFlags.NonPublic|BindingFlags.Instance).GetValue(v)) cases.Add(new[]{x}); }
        }
        foreach (var args in cases) {
          var o = Activator.CreateInstance(t);
          try { m.Invoke(o, args); pass++; Console.WriteLine($"PASS {t.Name}.{m.Name}({string.Join(",",args)})"); }
          catch (TargetInvocationException e) { fail++; Console.WriteLine($"FAIL {t.Name}.{m.Name}({string.Join(",",args)}): {e.InnerException.Message.Split('\n')[0]}"); }
        }
      }
    }
    Console.WriteLine($"pass={pass} fail={fail}"); return fail;
  }
}
