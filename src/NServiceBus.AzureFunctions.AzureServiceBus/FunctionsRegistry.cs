namespace NServiceBus;

static partial class FunctionsRegistry
{
    static partial void AddGeneratedFunctions(List<FunctionManifest> entries);

    public static IReadOnlyList<FunctionManifest> GetAll()
    {
        var list = new List<FunctionManifest>();
        AddGeneratedFunctions(list);
        return list;
    }
}