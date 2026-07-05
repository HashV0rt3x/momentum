using Momentum.Application.Common;
using Xunit;

namespace Momentum.UnitTests.Common;

public sealed class PagedRequestTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(3, 3)]
    public void Page_is_clamped_to_a_minimum_of_one(int input, int expected)
    {
        var request = new PagedRequest { Page = input };

        Assert.Equal(expected, request.Page);
    }

    [Theory]
    [InlineData(0, 50)]
    [InlineData(500, 200)]
    [InlineData(25, 25)]
    public void PageSize_is_clamped_between_default_and_max(int input, int expected)
    {
        var request = new PagedRequest { PageSize = input };

        Assert.Equal(expected, request.PageSize);
    }

    [Fact]
    public void Skip_is_computed_from_page_and_page_size()
    {
        var request = new PagedRequest { Page = 3, PageSize = 20 };

        Assert.Equal(40, request.Skip);
    }
}
