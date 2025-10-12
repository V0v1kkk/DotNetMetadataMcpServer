using DotNetMetadataMcpServer.Helpers;

namespace MetadataExplorerTest
{
    [TestFixture]
    [Parallelizable(ParallelScope.All)]
    public class PaginationTests
    {
        [Test]
        public void FilterAndPaginate_ShouldReturnEmpty_WhenPageSizeIsLessThanOne()
        {
            var items = new List<int> { 1, 2, 3, 4, 5 };
            var result = PaginationHelper.FilterAndPaginate(items, x => true, 1, 0);
            Assert.That(result.PaginatedItems, Is.Empty);
            Assert.That(result.AvailablePages, Is.Empty);
        }

        [Test]
        public void FilterAndPaginate_ShouldReturnEmpty_WhenPageNumberIsOutOfRange()
        {
            var items = new List<int> { 1, 2, 3, 4, 5 };
            var result = PaginationHelper.FilterAndPaginate(items, x => true, 10, 2);
            Assert.That(result.PaginatedItems, Is.Empty);
            Assert.That(result.AvailablePages, Is.EqualTo(new List<int> { 1, 2, 3 }));
        }

        [Test]
        public void FilterAndPaginate_ShouldReturnFilteredAndPaginatedItems()
        {
            var items = new List<int> { 1, 2, 3, 4, 5 };
            var result = PaginationHelper.FilterAndPaginate(items, x => x > 2, 1, 2);
            Assert.That(result.PaginatedItems, Is.EqualTo(new List<int> { 3, 4 }));
            Assert.That(result.AvailablePages, Is.EqualTo(new List<int> { 1, 2 }));
        }

        [Test]
        public void FilterAndPaginate_ShouldReturnAllItems_WhenFilterIsAlwaysTrue()
        {
            var items = new List<int> { 1, 2, 3, 4, 5 };
            var result = PaginationHelper.FilterAndPaginate(items, x => true, 1, 5);
            Assert.That(result.PaginatedItems, Is.EqualTo(items));
            Assert.That(result.AvailablePages, Is.EqualTo(new List<int> { 1 }));
        }

        [Test]
        public void FilterAndPaginate_ShouldReturnEmpty_WhenFilterExcludesAllItems()
        {
            var items = new List<int> { 1, 2, 3, 4, 5 };
            var result = PaginationHelper.FilterAndPaginate(items, x => false, 1, 5);
            Assert.That(result.PaginatedItems, Is.Empty);
            Assert.That(result.AvailablePages, Is.Empty);
        }
        
        [Test]
        public void FilterAndPaginate_ShouldReturnEmpty_WhenItemsListIsEmpty_AndFilterIsAlwaysTrue()
        {
            var items = new List<int>();
            var result = PaginationHelper.FilterAndPaginate(items, x => true, 1, 5);
            Assert.That(result.PaginatedItems, Is.Empty);
            Assert.That(result.AvailablePages, Is.Empty);
        }

        [Test]
        public void FilterAndPaginate_ShouldReturnPartialPage_WhenNotEnoughItemsForFullPage()
        {
            var items = new List<int> { 1, 2 };
            var result = PaginationHelper.FilterAndPaginate(items, x => true, 1, 5);
            Assert.That(result.PaginatedItems, Is.EqualTo(new List<int> { 1, 2 }));
            Assert.That(result.AvailablePages, Is.EqualTo(new List<int> { 1 }));
        }

        [Test]
        public void FilterAndPaginate_ShouldReturnLastPageItems_WhenPageNumberIsTotalPages()
        {
            var items = new List<int> { 1, 2, 3, 4, 5, 6 };
            var result = PaginationHelper.FilterAndPaginate(items, x => true, 2, 3);
            Assert.That(result.PaginatedItems, Is.EqualTo(new List<int> { 4, 5, 6 }));
            Assert.That(result.AvailablePages, Is.EqualTo(new List<int> { 1, 2 }));
        }
    }
}