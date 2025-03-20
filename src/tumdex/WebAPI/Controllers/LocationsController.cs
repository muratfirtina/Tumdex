using Application.Features.Locations.Dtos;
using Application.Features.Locations.Queries.Cities;
using Application.Features.Locations.Queries.Countries;
using Application.Features.Locations.Queries.Districts;
using Core.Application.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LocationsController : BaseController
    {
        [HttpGet("countries")]
        public async Task<IActionResult> GetAllCountries()
        {
            GetListResponse<CountryDto> result = await Mediator.Send(new GetListCountriesQuery());
            return Ok(result);
        }

        [HttpGet("countries/{id}")]
        public async Task<IActionResult> GetCountryById(int id)
        {
            CountryDto result = await Mediator.Send(new GetCountryByIdQuery { Id = id });
            return Ok(result);
        }

        // City endpoints
        [HttpGet("cities")]
        public async Task<IActionResult> GetAllCities()
        {
            GetListResponse<CityDto> result = await Mediator.Send(new GetListCitiesQuery());
            return Ok(result);
        }

        [HttpGet("cities/{id}")]
        public async Task<IActionResult> GetCityById(int id)
        {
            CityDto result = await Mediator.Send(new GetCityByIdQuery { Id = id });
            return Ok(result);
        }

        [HttpGet("countries/{countryId}/cities")]
        public async Task<IActionResult> GetCitiesByCountryId(int countryId)
        {
            GetListResponse<CityDto> result = await Mediator.Send(new GetCitiesByCountryIdQuery { CountryId = countryId });
            return Ok(result);
        }

        // District endpoints
        [HttpGet("districts")]
        public async Task<IActionResult> GetAllDistricts()
        {
            GetListResponse<DistrictDto> result = await Mediator.Send(new GetListDistrictsQuery());
            return Ok(result);
        }

        [HttpGet("districts/{id}")]
        public async Task<IActionResult> GetDistrictById(int id)
        {
            DistrictDto result = await Mediator.Send(new GetDistrictByIdQuery { Id = id });
            return Ok(result);
        }

        [HttpGet("cities/{cityId}/districts")]
        public async Task<IActionResult> GetDistrictsByCityId(int cityId)
        {
            GetListResponse<DistrictDto> result = await Mediator.Send(new GetDistrictsByCityIdQuery { CityId = cityId });
            return Ok(result);
        }
    }
}
