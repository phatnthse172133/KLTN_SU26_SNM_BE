using System;
using System.Threading.Tasks;
using ApplicationLayer.DTOs.Requests;
using ApplicationLayer.Services.FoodTags;
using ApplicationLayer.Helppers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using static DomainLayer.Enums.GeneralEnum;

namespace PresentationLayer.Controllers.Admin
{
    [ApiController]
    [Route("api/admin/food-tags")]
    [Authorize(Roles = "Admin")]
    public class AdminFoodTagController : ControllerBase
    {
        private readonly IFoodTagService _foodTagService;

        public AdminFoodTagController(IFoodTagService foodTagService)
        {
            _foodTagService = foodTagService;
        }

        [HttpGet]
        public async Task<IActionResult> GetPagedTags([FromQuery] FoodTagQueryRequest request, CancellationToken cancellationToken)
        {
            var result = await _foodTagService.GetAsync(request, cancellationToken);
            if (!result.Success)
            {
                return BadRequest(result);
            }
            return Ok(result);
        }

    }
}
