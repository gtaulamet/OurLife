using System.Globalization;
using OurLife.Localization.Icu;

namespace OurLife.Localization.Tests.Icu;

public class IcuMessageTests
{
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es");
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en");

    [Fact]
    public void Plain_text_is_returned_as_is()
    {
        Format("Nueva partida").ShouldBe("Nueva partida");
    }

    [Fact]
    public void Arguments_are_replaced()
    {
        Format("{colonist} tiene hambre.", ("colonist", "Elvira")).ShouldBe("Elvira tiene hambre.");
    }

    [Theory]
    [InlineData(0, "0 objetos")]
    [InlineData(1, "1 objeto")]
    [InlineData(2, "2 objetos")]
    public void Plural_chooses_the_case_and_writes_the_number(int count, string expected)
    {
        Format("{count, plural, one {# objeto} other {# objetos}}", ("count", count)).ShouldBe(expected);
    }

    [Fact]
    public void An_exact_plural_case_wins_over_the_category()
    {
        const string pattern = "{count, plural, =0 {ningún objeto} one {# objeto} other {# objetos}}";

        Format(pattern, ("count", 0)).ShouldBe("ningún objeto");
        Format(pattern, ("count", 1)).ShouldBe("1 objeto");
    }

    [Theory]
    [InlineData("female", "Elvira está agotada.")]
    [InlineData("male", "Elvira está agotado.")]
    [InlineData("unknown", "Elvira está agotado.")]
    public void Select_uses_the_matching_case_or_other(string gender, string expected)
    {
        Format(
            "{colonist} está {colonist_gender, select, female {agotada} other {agotado}}.",
            ("colonist", "Elvira"),
            ("colonist_gender", gender)).ShouldBe(expected);
    }

    [Fact]
    public void Pound_inside_a_select_nested_in_a_plural_uses_the_plural_number()
    {
        const string pattern =
            "{count, plural, one {# {gender, select, female {piedra} other {tronco}}} other {# {gender, select, female {piedras} other {troncos}}}}";

        Format(pattern, ("count", 3), ("gender", "female")).ShouldBe("3 piedras");
    }

    [Fact]
    public void Integers_have_no_thousands_separator_unless_asked()
    {
        Format("Año {year}", ("year", 1203)).ShouldBe("Año 1203");
        Format("{gold, number} monedas", ("gold", 12345)).ShouldBe("12.345 monedas");
        IcuMessage.Parse("{gold, number} coins")
            .Format(Args(("gold", 12345)), PluralRules.OneOther, English)
            .ShouldBe("12,345 coins");
    }

    [Fact]
    public void Apostrophes_are_text_unless_they_quote_syntax()
    {
        IcuMessage.Parse("Don't panic").Format(null, PluralRules.OneOther, English).ShouldBe("Don't panic");
        Format("Use '{' y '}' así, con ''comillas''").ShouldBe("Use { y } así, con 'comillas'");
    }

    [Fact]
    public void A_missing_argument_is_visible_instead_of_failing()
    {
        Format("{colonist} tiene hambre.").ShouldBe("⟦colonist⟧ tiene hambre.");
    }

    [Fact]
    public void Argument_names_are_collected_at_every_depth()
    {
        var message = IcuMessage.Parse(
            "{colonist} lleva {count, plural, one {# {item}} other {# {item_plural}}} ({colonist_gender, select, other {x}})");

        message.ArgumentNames.ShouldBe(new[] { "colonist", "colonist_gender", "count", "item", "item_plural" });
    }

    [Theory]
    [InlineData("{count, plural, one {# objeto}}", "'other'")]
    [InlineData("{name", "Missing")]
    [InlineData("texto } suelto", "Unexpected '}'")]
    [InlineData("{count, date}", "Unsupported argument type")]
    [InlineData("{count, plural, one {a} one {b} other {c}}", "Duplicate case")]
    [InlineData("{count, plural, several {a} other {b}}", "Unknown plural category")]
    [InlineData("{ , select, other {x}}", "argument name")]
    public void Invalid_patterns_are_rejected_with_a_clear_message(string pattern, string expectedFragment)
    {
        var error = Should.Throw<IcuSyntaxException>(() => IcuMessage.Parse(pattern));

        error.Message.ShouldContain(expectedFragment);
    }

    private static string Format(string pattern, params (string Name, object Value)[] arguments) =>
        IcuMessage.Parse(pattern).Format(Args(arguments), PluralRules.OneOther, Spanish);

    private static Dictionary<string, object?> Args(params (string Name, object Value)[] arguments) =>
        arguments.ToDictionary(a => a.Name, a => (object?)a.Value);
}
