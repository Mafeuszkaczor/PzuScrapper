using System;
using System.Collections.Generic;
using System.Text;

namespace Models
{
    public class SearchResponse
    {
        public int total { get; set; }
        public int pageNumber { get; set; }
        public int pageSize { get; set; }
        public int totalPages { get; set; }
        public List<Car> result { get; set; }
    }
}
