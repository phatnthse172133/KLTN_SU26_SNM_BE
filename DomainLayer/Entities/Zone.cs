using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DomainLayer.Entities
{

    // Khu vực trong Night Market
    // Ví dụ: Food Zone, Drink Zone, Dessert Zone,...
    public partial class Zone
    {
        public Guid Id { get; set; }

        public Guid NightMarketId { get; set; }

        public string ZoneName { get; set; } = null!;

        public string? Description { get; set; }

        public string? ColorCode { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public virtual NightMarket NightMarket { get; set; } = null!;

        public virtual ICollection<LayoutNode> LayoutNodes { get; set; } = new List<LayoutNode>();

        public virtual ICollection<BoothRegistration> BoothRegistrations { get; set; } = new List<BoothRegistration>();
    }
}
