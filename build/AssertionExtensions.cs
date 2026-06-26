namespace _build;

static class AssertionExtensions
{
    public static void ShouldEqual(this string actual, string expected, string message) =>
        _ = actual == expected ? true : throw new InvalidOperationException($"{message} Expected '{expected}', actual '{actual}'.");
}