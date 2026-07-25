using System.Threading.Tasks;

namespace DomainLayer.InterfaceRepository
{
    public interface ISequenceRepository
    {
        Task<long> NextPayOSOrderCodeAsync();
    }
}
