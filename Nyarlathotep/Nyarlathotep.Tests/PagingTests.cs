using Nyarlathotep.Logic;

namespace Nyarlathotep.Tests;

/// <summary>raphael-api-core D3: contract §4 paging.</summary>
public class PagingTests
{
    static List<string> Rows(int n) => Enumerable.Range(1, n).Select(i => $"[NYAR:def] id=e{i:00}").ToList();

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("+1")]
    [InlineData(" 1")]
    [InlineData("x")]
    [InlineData("1.5")]
    [InlineData("99999999999")]
    [InlineData("2147483648")]
    [InlineData("١")]   // an Arabic-Indic digit
    public void A_bad_page_is_badarg_alone(string raw)
    {
        var reply = Paging.Reply("events", Rows(12), raw);
        Assert.Equal(["[NYAR:err] cmd=events code=badarg arg=page"], reply);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1")]
    public void No_page_means_page_1(string? raw)
    {
        var reply = Paging.Reply("events", Rows(12), raw);
        Assert.Equal(11, reply.Count);
        Assert.Equal("[NYAR:def] id=e01", reply[0]);
        Assert.Equal("[NYAR:end] cmd=events page=1/2 count=12", reply[^1]);
    }

    [Fact]
    public void An_empty_set_is_page_1_of_1_count_0()
    {
        Assert.Equal(["[NYAR:end] cmd=events page=1/1 count=0"], Paging.Reply("events", [], null));
    }

    [Fact]
    public void Ten_rows_are_one_page_and_page_2_is_past_the_last()
    {
        Assert.Equal("[NYAR:end] cmd=events page=1/1 count=10", Paging.Reply("events", Rows(10), "1")[^1]);
        Assert.Equal(["[NYAR:end] cmd=events page=2/1 count=10"], Paging.Reply("events", Rows(10), "2"));
    }

    [Fact]
    public void Page_2_of_11_rows_is_the_eleventh_alone()
    {
        Assert.Equal(["[NYAR:def] id=e11", "[NYAR:end] cmd=events page=2/2 count=11"], Paging.Reply("events", Rows(11), "2"));
    }

    [Fact]
    public void The_largest_int_page_is_past_the_last_without_overflow()
    {
        Assert.Equal([$"[NYAR:end] cmd=events page={int.MaxValue}/3 count=24"], Paging.Reply("events", Rows(24), int.MaxValue.ToString()));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(10, 1)]
    [InlineData(11, 2)]
    [InlineData(24, 3)]
    [InlineData(int.MaxValue, 214748365)]
    public void Pages_is_at_least_1(int count, int pages) => Assert.Equal(pages, Paging.Pages(count));
}
