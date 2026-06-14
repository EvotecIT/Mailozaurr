using Xunit;

namespace Mailozaurr.Tests;

/// <summary>
/// Collection used for tests that mutate shared static state.
/// Parallelization is disabled to avoid interference.
/// </summary>
[CollectionDefinition("GraphCollection", DisableParallelization = true)]
public class GraphTestCollection { }