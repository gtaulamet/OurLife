namespace OurLife.Content.Tests;

/// <summary>The catalogs are UTF-8 once: a text written through another encoding shows as "Ã±" for "ñ".</summary>
public class EncodingTests
{
    [Fact]
    public void No_text_of_the_catalogs_is_double_encoded()
    {
        var files = Directory.GetFiles(Path.Combine(RepoPaths.Content, "locales"), "*.json", SearchOption.AllDirectories);

        files.ShouldNotBeEmpty();
        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            text.ShouldNotContain("Ã", customMessage: file);
            text.ShouldNotContain("Â", customMessage: file);
            text.ShouldNotContain("\uFFFD", customMessage: file);
        }
    }
}
