using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Agrumy.Web.Controllers.View
{
    /// Nav placeholder only - Fruit Plantation has no entities/catalog/CRUD yet, this just gives the information architecture a visible home for later.
    [Authorize]
    public class FruitController : Controller
    {
        public ActionResult Index() => View();
    }
}
