namespace NServiceBus;

static partial class FunctionsRegistry
{
    static partial void AddGeneratedFunctions(List<FunctionManifest> entries);

    public static bool SourceGeneratorEnabled { get; private set; }

    public static IReadOnlyList<FunctionManifest> GetAll()
    {
        if (allFunctions is not null)
        {
            return allFunctions;
        }

        allFunctions = [];
        AddGeneratedFunctions(allFunctions);
        return allFunctions;
    }

    static List<FunctionManifest>? allFunctions;
}