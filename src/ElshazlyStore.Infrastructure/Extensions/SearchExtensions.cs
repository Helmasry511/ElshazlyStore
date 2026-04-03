using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace ElshazlyStore.Infrastructure.Extensions;

/// <summary>
/// Provider-aware search: ILIKE on PostgreSQL (leverages gin_trgm_ops GIN indexes),
/// LIKE + LOWER on SQLite / other providers.
/// </summary>
public static class SearchExtensions
{
    private static readonly MethodInfo LikeMethod =
        typeof(DbFunctionsExtensions).GetMethod(
            nameof(DbFunctionsExtensions.Like),
            new[] { typeof(DbFunctions), typeof(string), typeof(string) })!;

    private static readonly MethodInfo? ILikeMethod =
        typeof(NpgsqlDbFunctionsExtensions).GetMethod(
            nameof(NpgsqlDbFunctionsExtensions.ILike),
            new[] { typeof(DbFunctions), typeof(string), typeof(string) });

    private static readonly MethodInfo ToLowerMethod =
        typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!;

    /// <summary>
    /// Applies a case-insensitive LIKE/ILIKE search across the specified columns.
    /// <para>
    /// PostgreSQL  → <c>col ILIKE '%term%'</c> (leverages gin_trgm_ops GIN indexes on raw columns).<br/>
    /// SQLite/other → <c>LOWER(col) LIKE '%term%'</c>.
    /// </para>
    /// Every column gets an automatic IS NOT NULL guard so nullable columns
    /// and optional navigation properties are handled safely.
    /// </summary>
    public static IQueryable<T> ApplySearch<T>(
        this IQueryable<T> source,
        DatabaseFacade database,
        string? searchTerm,
        params Expression<Func<T, string?>>[] columns)
    {
        if (string.IsNullOrWhiteSpace(searchTerm) || columns.Length == 0)
            return source;

        var useILike = database.IsNpgsql() && ILikeMethod is not null;
        var trimmed = searchTerm.Trim();
        var pattern = useILike ? $"%{trimmed}%" : $"%{trimmed.ToLowerInvariant()}%";

        var param = Expression.Parameter(typeof(T), "x");
        var efFunctions = Expression.Property(null, typeof(EF), nameof(EF.Functions));
        var patternConst = Expression.Constant(pattern);

        Expression? combined = null;

        foreach (var col in columns)
        {
            // Rebind column body to the shared parameter
            var body = new ParameterReplacer(col.Parameters[0], param).Visit(col.Body);

            // Build: ILIKE(col, pattern) -or- LIKE(LOWER(col), pattern)
            Expression matchExpr;
            if (useILike)
            {
                matchExpr = Expression.Call(ILikeMethod!, efFunctions, body, patternConst);
            }
            else
            {
                var lowered = Expression.Call(body, ToLowerMethod);
                matchExpr = Expression.Call(LikeMethod, efFunctions, lowered, patternConst);
            }

            // Null guard: col IS NOT NULL AND <match>
            // Redundant for non-nullable columns but the DB optimizer eliminates it.
            var nullCheck = Expression.NotEqual(body, Expression.Constant(null, typeof(string)));
            matchExpr = Expression.AndAlso(nullCheck, matchExpr);

            combined = combined is null ? matchExpr : Expression.OrElse(combined, matchExpr);
        }

        var predicate = Expression.Lambda<Func<T, bool>>(combined!, param);
        return source.Where(predicate);
    }

    private sealed class ParameterReplacer : ExpressionVisitor
    {
        private readonly ParameterExpression _old;
        private readonly ParameterExpression _new;

        public ParameterReplacer(ParameterExpression old, ParameterExpression @new)
        {
            _old = old;
            _new = @new;
        }

        protected override Expression VisitParameter(ParameterExpression node)
            => node == _old ? _new : base.VisitParameter(node);
    }
}
