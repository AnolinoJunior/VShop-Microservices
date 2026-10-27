using Vshop.Web.Models;
using VShop.Web.Models;

namespace Vshop.Web.Services.Contracts
{
    public interface ICategoryService
    {
        Task<IEnumerable<CategoryViewModel>?> GetAllCategories(string token);
    }
}