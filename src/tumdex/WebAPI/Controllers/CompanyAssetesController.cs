using Application.Consts;
using Application.CustomAttributes;
using Application.Dtos.Assets;
using Application.Enums;
using Application.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CompanyAssetesController : BaseController
    {
        private readonly ICompanyAssetService _companyAssetService;

        public CompanyAssetesController(ICompanyAssetService companyAssetService)
        {
            _companyAssetService = companyAssetService;
        }
        
        [HttpPost("logo")]
        [Authorize(AuthenticationSchemes = "Admin")]
        [AuthorizeDefinition (ActionType = ActionType.Updating, Definition = "Update Company Logo", Menu = AuthorizeDefinitionConstants.Assets)]
        public async Task<IActionResult> UploadLogo(IFormFile file)
        {
            try
            {
                var result = await _companyAssetService.UploadLogoAsync(file);
                return Ok(new { success = result });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
        [HttpGet("logo")]
        public async Task<ActionResult<CompanyLogoDto>> GetLogo()
        {
            var logo = await _companyAssetService.GetLogoAsync();
            return Ok(logo);
        }

        [HttpPut("logo")]
        [Authorize(AuthenticationSchemes = "Admin")]
        [AuthorizeDefinition(ActionType = ActionType.Updating, Definition = "Update Company Logo", Menu = AuthorizeDefinitionConstants.Assets)]
        public async Task<IActionResult> UpdateLogo(IFormFile file)
        {
            try
            {
                var result = await _companyAssetService.UpdateLogoAsync(file);
                return Ok(new { success = result });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}
