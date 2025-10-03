namespace DotNetMetadataMcpServer.Helpers
{
    public static class PaginationHelper
    {
        public static (List<T> PaginatedItems, List<int> AvailablePages) FilterAndPaginate<T>(IEnumerable<T> items, 
            Func<T, bool> filter, int pageNumber, int pageSize)
        {
            if (pageSize < 1)
                return ([], []);
            
            var filtered = items.Where(filter).ToList();
            var totalItems = filtered.Count;
            var totalPages = (totalItems + pageSize - 1) / pageSize;
            var availablePages = Enumerable.Range(1, totalPages).ToList();
            
            if (pageNumber < 1 || pageNumber > totalPages)
                return ([], availablePages);
            
            var paginated = filtered.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
            return (paginated, availablePages);
        }
    }
}