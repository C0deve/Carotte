using System.Collections.Concurrent;
using System.Reflection;

namespace Carotte;

/// <summary>
/// Resolves message types and their identifiers for serialization, routing, and dispatching.
/// </summary>
/// <remarks>
/// ⚠️ <b>Important:</b> Ensure that publishers and consumers communicating across services use the same (or compatible)
/// <see cref="IMessageTypeResolver"/> implementation to guarantee proper message dispatching.
/// </remarks>
public class MessageTypeResolver : IMessageTypeResolver
{
    /// <summary>
    /// Gets the default singleton instance of <see cref="MessageTypeResolver"/>.
    /// </summary>
    public static MessageTypeResolver Default { get; } = new();

    private readonly ConcurrentDictionary<Type, TypeMetadata> _metadataCache = new();

    /// <summary>
    /// Gets the type identifier for the specified message type, using the <see cref="MessageTypeAttribute"/> if present,
    /// or defaulting to the simple type name.
    /// </summary>
    /// <param name="messageType">The message type.</param>
    /// <returns>The string identifier representing the message type.</returns>
    public string GetTypeIdentifier(Type messageType)
    {
        ArgumentNullException.ThrowIfNull(messageType);
        return GetMetadata(messageType).Identifier;
    }

    /// <summary>
    /// Resolves a matching <see cref="Type"/> from a type identifier among a collection of candidate types.
    /// </summary>
    /// <param name="typeIdentifier">The incoming type identifier (name, full name, AQN, URN, or custom alias).</param>
    /// <param name="candidateTypes">The candidate message types to resolve against.</param>
    /// <returns>The resolved <see cref="Type"/>, or <c>null</c> if no unique or valid match is found.</returns>
    public Type? ResolveType(string? typeIdentifier, IEnumerable<Type>? candidateTypes)
    {
        if (candidateTypes is null) return null;

        // When no type identifier is provided, resolve only if exactly one candidate type exists
        if (string.IsNullOrWhiteSpace(typeIdentifier))
        {
            return candidateTypes switch
            {
                IReadOnlyList<Type> { Count: 1 } list => list[0],
                IReadOnlyList<Type> => null,
                _ => ResolveSingleCandidate(candidateTypes)
            };
        }

        var trimmedIdentifier = typeIdentifier.Trim();
        var (cleanTypeName, cleanShortName) = ExtractCleanTypeNames(trimmedIdentifier);

        // Find the first candidate whose metadata matches the identifier
        return candidateTypes.FirstOrDefault(candidate => 
            GetMetadata(candidate)
            .Matches(trimmedIdentifier,
                cleanTypeName,
                cleanShortName));
    }

    private static Type? ResolveSingleCandidate(IEnumerable<Type> candidateTypes)
    {
        using var enumerator = candidateTypes.GetEnumerator();
        if (!enumerator.MoveNext()) return null;
        var first = enumerator.Current;
        return enumerator.MoveNext() ? null : first;
    }

    private TypeMetadata GetMetadata(Type type) => _metadataCache.GetOrAdd(type, static t =>
    {
        var customName = t.GetCustomAttribute<MessageTypeAttribute>()?.Name;
        return new TypeMetadata(
            Name: t.Name,
            FullName: t.FullName,
            CustomAlias: string.IsNullOrWhiteSpace(customName) ? null : customName);
    });

    /// <summary>
    /// Extracts cleaned type name and short name from an Assembly-Qualified Name (AQN) or URN format.
    /// </summary>
    private static (string? TypeName, string? ShortName) ExtractCleanTypeNames(string identifier) => identifier switch
    {
        // 1. Assembly-Qualified Name (AQN), e.g. "Namespace.Type, Assembly, Version=..."
        _ when identifier.IndexOf(',') is > 0 and var commaIdx =>
            ExtractAqnNames(identifier[..commaIdx].Trim()),

        // 2. Uniform Resource Name (URN), e.g. "urn:message:Namespace:Type"
        _ when identifier.StartsWith("urn:", StringComparison.OrdinalIgnoreCase) &&
               identifier.LastIndexOf(':') is >= 0 and var lastColon &&
               lastColon < identifier.Length - 1 =>
            ExtractUrnNames(identifier[(lastColon + 1)..].Trim()),

        // Unrecognized format
        _ => (null, null)
    };

    private static (string? TypeName, string? ShortName) ExtractAqnNames(string typePart) =>
        typePart.LastIndexOf('.') switch
        {
            >= 0 and var lastDot => (typePart, typePart[(lastDot + 1)..]),
            _ => (typePart, typePart)
        };

    private static (string? TypeName, string? ShortName) ExtractUrnNames(string shortName) =>
        (shortName, shortName);

    /// <summary>
    /// Holds cached metadata of a message type for fast resolution.
    /// </summary>
    private readonly record struct TypeMetadata(string Name, string? FullName, string? CustomAlias)
    {
        /// <summary>
        /// Gets the primary identifier (custom alias if defined, otherwise the simple type name).
        /// </summary>
        public string Identifier => CustomAlias ?? Name;

        /// <summary>
        /// Checks if the current type metadata matches the given identifier and cleaned names.
        /// </summary>
        public bool Matches(string identifier, string? cleanTypeName, string? cleanShortName) => this switch
        {
            // Direct match with custom alias (from [MessageType] attribute)
            { CustomAlias: { } alias } when EqualsIgnoreCase(alias, identifier) => true,

            // Direct match with simple type name (e.g. "OrderCreated")
            _ when EqualsIgnoreCase(Name, identifier) => true,

            // Direct match with full type name (e.g. "MyApp.Events.OrderCreated")
            { FullName: { } fullName } when EqualsIgnoreCase(fullName, identifier) => true,

            // Match with clean type name extracted from Assembly-Qualified Name (AQN)
            _ when cleanTypeName is { } typeName && (EqualsIgnoreCase(Name, typeName) || (FullName is { } fn && EqualsIgnoreCase(fn, typeName))) => true,

            // Match with clean short name extracted from URN or AQN
            _ when cleanShortName is { } shortName && EqualsIgnoreCase(Name, shortName) => true,

            // No match found
            _ => false
        };

        private static bool EqualsIgnoreCase(string? left, string? right) =>
            string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }
}
