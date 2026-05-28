#nullable enable

using Microsoft.CodeAnalysis;
using Trecs.SourceGen.Performance;

namespace Trecs.SourceGen.Shared
{
    /// <summary>
    /// Routing helper for the <c>[ForEachEntity]</c> / <c>[FromSingleEntity]</c> markers.
    /// <para>
    /// <c>[ForEachEntity]</c> is a method-level iteration attribute. The aspect-vs-components
    /// kind is determined by inspecting the method's parameter types.
    /// </para>
    /// <para>
    /// <c>[FromSingleEntity]</c> (and its shorthand <c>[FromGlobalEntity]</c>) is a per-parameter
    /// (and per-field) attribute that resolves to the unique entity matching the inline
    /// tag(s). A method whose only iteration parameters are <c>[FromSingleEntity]</c>-annotated
    /// runs once (RunOnceGenerator). A method that mixes <c>[FromSingleEntity]</c> parameters
    /// with <c>[ForEachEntity]</c> hoists the singletons before the iteration loop.
    /// </para>
    /// </summary>
    internal static class IterationAttributeRouting
    {
        /// <summary>
        /// True if the symbol carries <c>[FromSingleEntity]</c> or <c>[FromGlobalEntity]</c>.
        /// </summary>
        public static bool HasFromSingleEntityAttribute(ISymbol symbol) =>
            PerformanceCache.HasAttributeByName(
                symbol,
                TrecsAttributeNames.FromSingleEntity,
                TrecsNamespaces.Trecs
            )
            || PerformanceCache.HasAttributeByName(
                symbol,
                TrecsAttributeNames.FromGlobalEntity,
                TrecsNamespaces.Trecs
            );

        /// <summary>
        /// Returns true if the method has at least one iteration-target parameter whose
        /// type implements <c>Trecs.IAspect</c> AND is not marked <c>[FromSingleEntity]</c> /
        /// <c>[FromGlobalEntity]</c> / <c>[PassThroughArgument]</c>. Used by aspect-iteration
        /// generators to decide whether to claim a method.
        /// </summary>
        public static bool HasAspectParameter(IMethodSymbol method)
        {
            foreach (var p in method.Parameters)
            {
                if (
                    PerformanceCache.HasAttributeByName(
                        p,
                        TrecsAttributeNames.PassThroughArgument,
                        TrecsNamespaces.Trecs
                    )
                )
                    continue;
                if (HasFromSingleEntityAttribute(p))
                    continue;
                if (SymbolAnalyzer.ImplementsInterface(p.Type, "IAspect", TrecsNamespaces.Trecs))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// True if the method carries the <c>[ForEachEntity]</c> attribute.
        /// </summary>
        public static bool HasEntityFilter(IMethodSymbol method) =>
            PerformanceCache.HasAttributeByName(
                method,
                TrecsAttributeNames.ForEachEntity,
                TrecsNamespaces.Trecs
            );

        /// <summary>
        /// True if any parameter on the method carries <c>[FromSingleEntity]</c> or
        /// <c>[FromGlobalEntity]</c>.
        /// </summary>
        public static bool HasFromSingleEntityParameter(IMethodSymbol method)
        {
            foreach (var p in method.Parameters)
            {
                if (HasFromSingleEntityAttribute(p))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// True if the method should be claimed by RunOnceGenerator: has at least one
        /// <c>[FromSingleEntity]</c> / <c>[FromGlobalEntity]</c> parameter, no <c>[ForEachEntity]</c>,
        /// no <c>[WrapAsJob]</c>. Such a method is generated as a single-shot
        /// (<c>WorldAccessor</c>) overload that hoists each singleton then calls the user
        /// method exactly once.
        /// </summary>
        public static bool IsRunOnceMethod(IMethodSymbol method) =>
            HasFromSingleEntityParameter(method)
            && !HasEntityFilter(method)
            && !HasWrapAsJobAttribute(method);

        /// <summary>
        /// True if a method that wears <c>[ForEachEntity]</c> should be claimed by an
        /// aspect-iteration generator. Returns true iff at least one non-singleton
        /// parameter implements <c>IAspect</c>.
        /// </summary>
        public static bool RoutesToAspectGenerator(IMethodSymbol method) =>
            HasAspectParameter(method);

        /// <summary>
        /// True if a method that wears <c>[ForEachEntity]</c> should be claimed by a
        /// component-iteration generator. Returns true iff no non-singleton parameter
        /// implements <c>IAspect</c>.
        /// </summary>
        public static bool RoutesToComponentsGenerator(IMethodSymbol method) =>
            !HasAspectParameter(method);

        /// <summary>
        /// True if the method carries the <c>[WrapAsJob]</c> attribute. Methods with this
        /// attribute are claimed by the AutoJobGenerator and should be skipped by
        /// ForEach generators (component or aspect mode).
        /// </summary>
        public static bool HasWrapAsJobAttribute(IMethodSymbol method) =>
            PerformanceCache.HasAttributeByName(
                method,
                TrecsAttributeNames.WrapAsJob,
                TrecsNamespaces.Trecs
            );
    }
}
