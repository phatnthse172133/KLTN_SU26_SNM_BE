using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static DomainLayer.Enums.GeneralEnum;

namespace DomainLayer.Entities
{
    public partial class BoothRegistration
    {
        public Guid Id { get; set; }

        public Guid OwnerId { get; set; }

        public Guid RequestedNightMarketId { get; set; }

        public Guid? PreferredZoneId { get; set; }

        public Guid? PreferredLayoutNodeId { get; set; }

        public string BoothName { get; set; } = null!;

        public string? PhoneNumber { get; set; }

        public string? Description { get; set; }

        public string? RejectReason { get; set; }

        public BoothRegistrationStatus Status { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public virtual User Owner { get; set; } = null!;

        public virtual NightMarket RequestedNightMarket { get; set; } = null!;

        public virtual Zone? PreferredZone { get; set; }

        public virtual LayoutNode? PreferredLayoutNode { get; set; }

        public virtual ICollection<BoothDocument> BoothDocuments { get; set; } = new List<BoothDocument>();

        public virtual Booth? Booth { get; set; }


    }
}
