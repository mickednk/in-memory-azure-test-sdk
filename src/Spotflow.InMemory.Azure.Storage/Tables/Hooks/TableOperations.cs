namespace Spotflow.InMemory.Azure.Storage.Tables.Hooks;

[Flags]
public enum TableOperations
{
    None = 0,
    Create = 1,
    All = Create
}