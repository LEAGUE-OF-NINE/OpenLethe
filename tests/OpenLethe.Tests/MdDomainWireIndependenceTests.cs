using System.Reflection;
using OpenLethe.Server.MirrorDungeon.Model;

namespace OpenLethe.Tests;

// Proves the foundation's end-state invariants: the domain model is wire-independent and the
// Raw* scaffolding is gone.
public class MdDomainWireIndependenceTests
{
    [Fact]
    public void ModelTypes_ReferenceNoWireType_AndHaveNoRawSlots()
    {
        var modelTypes = typeof(Run).Assembly.GetTypes()
            .Where(t => t.Namespace == "OpenLethe.Server.MirrorDungeon.Model" && t.IsClass)
            .ToList();
        Assert.NotEmpty(modelTypes);

        var offenders = new List<string>();
        foreach (var t in modelTypes)
        {
            foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (p.Name.StartsWith("Raw"))
                    offenders.Add($"{t.Name}.{p.Name} (Raw* scaffolding still present)");

                // Any property whose type (or a generic arg, e.g. List<Wire.X>) lives in the
                // wire namespace is a wire leak.
                var referenced = new[] { p.PropertyType }
                    .Concat(p.PropertyType.IsGenericType ? p.PropertyType.GetGenericArguments() : Array.Empty<Type>());
                foreach (var rt in referenced)
                    if (rt.Namespace == "OpenLethe.Server.Wire")
                        offenders.Add($"{t.Name}.{p.Name} : {rt.Name} (wire leak)");
            }
        }

        Assert.True(offenders.Count == 0, string.Join("\n", offenders));
    }
}
