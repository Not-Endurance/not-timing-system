using System.Runtime.CompilerServices;
using Not.Storage;

namespace NTS.Tests.Integration.Harness;

/// <summary>
/// The Mongo driver keeps one serializer registry for the whole process. A test that reads a document before the Api starts
/// caches the driver's default for a type, and then the Api's own serializers cannot be registered: the host fails to start,
/// or a Guid or a time is stored the wrong way (ADR-0014). The test process registers the Api's serializers before any test
/// runs, so which test happens to run first does not decide them.
/// </summary>
internal static class MongoSerializers
{
    [ModuleInitializer]
    internal static void Register()
    {
        NStorageBuilder.RegisterSerializers();
    }
}
