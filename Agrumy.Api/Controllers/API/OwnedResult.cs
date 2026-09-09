using Microsoft.AspNetCore.Mvc;

namespace Agrumy.Api.Controllers.API
{
    /// Named replacement for the (T? Entity, ActionResult? Error) tuple every EnsureOwned* helper used to
    /// return - Deconstruct keeps every existing `var (entity, error) = await EnsureOwnedXAsync(...)` call
    /// site compiling unchanged, and the implicit tuple conversion keeps every EnsureOwned* method body a
    /// one-line `return (entity, error);` as before, so only the ~18 EnsureOwned* method signatures
    /// themselves needed to change.
    public readonly struct OwnedResult<T> where T : class
    {
        public T? Entity { get; }
        public ActionResult? Error { get; }

        public OwnedResult(T? entity, ActionResult? error)
        {
            Entity = entity;
            Error = error;
        }

        public void Deconstruct(out T? entity, out ActionResult? error)
        {
            entity = Entity;
            error = Error;
        }

        public static implicit operator OwnedResult<T>((T? Entity, ActionResult? Error) tuple) => new(tuple.Entity, tuple.Error);
    }
}
