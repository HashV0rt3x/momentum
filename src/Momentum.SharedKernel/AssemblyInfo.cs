using System.Runtime.CompilerServices;

// Only Momentum.Infrastructure (the DbContext's SaveChanges override) is allowed to
// stamp Entity.CreatedAtUtc/UpdatedAtUtc directly — module code sets everything else
// through public members.
[assembly: InternalsVisibleTo("Momentum.Infrastructure")]
