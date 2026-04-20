using System.Collections.Generic;

namespace PzuScrapper.Models.Response;

public class SearchResponse
{
    public int total { get; set; }
    public int pageNumber { get; set; }
    public int pageSize { get; set; }
    public int totalPages { get; set; }
    /// <summary>API list rows — must be <see cref="global::Models.Car"/>, not the <c>PzuScrapper.Models.Car</c> namespace.</summary>
    public List<global::Models.Car> result { get; set; }
}
