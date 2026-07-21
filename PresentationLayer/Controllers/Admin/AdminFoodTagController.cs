using System;
using System.Threading.Tasks;
using ApplicationLayer.AI.DTOs;
using ApplicationLayer.AI.Services;
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

        [HttpPost]
        public async Task<IActionResult> CreateTag([FromBody] CreateFoodTagRequest request, CancellationToken cancellationToken)
        {
            var result = await _foodTagService.CreateAsync(request, cancellationToken);
            if (!result.Success)
            {
                return BadRequest(result);
            }
            return Ok(result);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateTag(Guid id, [FromBody] UpdateFoodTagRequest request, CancellationToken cancellationToken)
        {
            var result = await _foodTagService.UpdateAsync(id, request, cancellationToken);
            if (!result.Success)
            {
                return BadRequest(result);
            }
            return Ok(result);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteTag(Guid id, CancellationToken cancellationToken)
        {
            var result = await _foodTagService.DeleteAsync(id, cancellationToken);
            if (!result.Success)
            {
                return BadRequest(result);
            }
            return Ok(result);
        }
    }
}
