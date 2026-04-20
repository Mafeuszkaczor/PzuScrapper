using System;
using System.Collections.Generic;
using System.Text;

namespace Models
{
    public class Pageable
    {
        public int page { get; set; }
        public int size { get; set; }
        public Sort sort { get; set; }
    }
}
