using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;

if (args.Length != 1 || !Directory.Exists(args[0]))
{
    Console.Error.WriteLine("Usage: PayrollLoanCompatibilityProbe <copied-deployment-directory>");
    return 2;
}

var path = Path.GetFullPath(args[0]);
AssemblyLoadContext.Default.Resolving += (_, name) =>
{
    var file = Path.Combine(path, name.Name + ".dll");
    return File.Exists(file) ? AssemblyLoadContext.Default.LoadFromAssemblyPath(file) : null;
};

try
{
    var domain = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(path, "Mightyfin.Erp.Hrm.Domain.dll"));
    var advance = domain.GetType("Mightyfin.Erp.Hrm.Domain.Entities.SalaryAdvance", true)!;
    Console.WriteLine($"SalaryAdvance.OpeningRecoveredAmount exists: {advance.GetProperty("OpeningRecoveredAmount") is not null}");
    Console.WriteLine($"SalaryAdvance.RecordType exists: {advance.GetProperty("RecordType") is not null}");
    var infrastructure = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(path, "Mightyfin.Erp.Hrm.Infrastructure.dll"));
    var target = infrastructure.GetTypes().Single(x => x.Name.Contains("<GetSalaryAdvanceRecoveredAmountsAsync>"));
    var moveNext = target.GetMethod("MoveNext", BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new InvalidOperationException("Loan recovery state machine was not found.");
    // JIT the actual deployed method without calling it or opening a database.
    RuntimeHelpers.PrepareMethod(moveNext.MethodHandle);
    Console.WriteLine("Loan recovery method JIT succeeded.");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception.GetType().Name + ": " + exception.Message);
    return 1;
}
