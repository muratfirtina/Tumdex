using System.Diagnostics;
using Application.Abstraction.Services;
using Application.Consts;
using Application.CustomAttributes;
using Application.Enums;
using Application.Features.Users.Commands.AssignRoleToUser;
using Application.Features.Users.Commands.ChangePassword;
using Application.Features.Users.Commands.CreateUser;
using Application.Features.Users.Commands.LoginUser;
using Application.Features.Users.Commands.UpdateForgetPassword;
using Application.Features.Users.Queries.GetAllUsers;
using Application.Features.Users.Queries.GetByDynamic;
using Application.Features.Users.Queries.GetCurrentUser;
using Application.Features.Users.Queries.GetRolesToUser;
using Application.Features.Users.Queries.IsAdmin;
using Core.Application.Requests;
using Core.Application.Responses;
using Core.Persistence.Dynamic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : BaseController
    {
        private readonly IMetricsService _metricsService;

        public UsersController(IMetricsService metricsService)
        {
            _metricsService = metricsService;
        }

        [HttpGet]
        [Authorize(AuthenticationSchemes = "Admin")]
        [AuthorizeDefinition(ActionType = ActionType.Reading, Definition = "Get All Users")]
        public async Task<IActionResult> GetList([FromQuery] PageRequest pageRequest)
        {
            GetListResponse<GetAllUsersQueryResponse> response = await Mediator.Send(new GetAllUsersQuery { PageRequest = pageRequest });
            return Ok(response);
        }
        
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateUserCommand createUserCommand)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                CreatedUserResponse response = await Mediator.Send(createUserCommand);
                stopwatch.Stop();
        
                // Kullanıcı kayıt metrikleri
                _metricsService.UpdateActiveUsers("new_registration", 1);
                _metricsService.RecordRequestDuration(
                    "user_registration",
                    "create",
                    stopwatch.Elapsed.TotalMilliseconds);
        
                return Created(uri: "", response);
            }
            catch (Exception ex)
            {
                _metricsService.RecordSecurityEvent(
                    "registration_failure",
                    "warning");
                throw;
            }
        }
        
        [HttpPost("update-forgot-password")]
        public async Task<IActionResult>UpdateForgotPassword(UpdateForgotPasswordRequest updateForgotPasswordRequest)
        {
            var response = await Mediator.Send(updateForgotPasswordRequest);
            return Ok(response);
        }
        
        [HttpPost("assign-role-to-user")]
        [Authorize(AuthenticationSchemes = "Admin")]
        [AuthorizeDefinition(ActionType = ActionType.Updating, Definition = "Assign Role To User", Menu = AuthorizeDefinitionConstants.Users)]
        public async Task<IActionResult> AssignRoleToUser(AssignRoleToUserRequest assignRoleToUserRequest)
        {
            var response = await Mediator.Send(assignRoleToUserRequest);
            return Ok(response);
        }
        
        [HttpGet("get-roles-to-user/{UserId}")]
        [Authorize(AuthenticationSchemes = "Admin")]
        [AuthorizeDefinition(ActionType = ActionType.Reading, Definition = "Get Roles To User", Menu = AuthorizeDefinitionConstants.Users)]
        public async Task<IActionResult> GetRolesToUser([FromRoute]GetRolesToUserQuery getRolesToUserQuery)
        {
            var response = await Mediator.Send(getRolesToUserQuery);
            return Ok(response);
        }
        
        [HttpPost("GetList/ByDynamic")]
        [Authorize(AuthenticationSchemes = "Admin")]
        [AuthorizeDefinition(ActionType = ActionType.Reading, Definition = "Get List User By Dynamic", Menu = AuthorizeDefinitionConstants.Users)]
        public async Task<IActionResult> GetListByDynamic([FromQuery] PageRequest pageRequest, [FromBody] DynamicQuery? dynamicQuery = null)
        {
            GetListResponse<GetListUserByDynamicQueryResponse> response = await Mediator.Send(new GetListUserByDynamicQuery { DynamicQuery = dynamicQuery, PageRequest = pageRequest });
            return Ok(response);
        }
        
        [HttpGet("is-admin")]
        [Authorize(AuthenticationSchemes = "Admin")]
        [AuthorizeDefinition(ActionType = ActionType.Reading, Definition = "Is Admin", Menu = AuthorizeDefinitionConstants.Users)]
        public async Task<IActionResult> IsAdmin([FromQuery] IsUserAdminQuery isUserAdminQuery)
        {
            var response = await Mediator.Send(isUserAdminQuery);
            return Ok(response);
        }
        
        [HttpGet("current-user")]
        [Authorize(AuthenticationSchemes = "Admin")]
        [AuthorizeDefinition(ActionType = ActionType.Reading, Definition = "Get Current User", Menu = AuthorizeDefinitionConstants.Users)]
        public async Task<IActionResult> GetCurrentUser()
        {
            var userName = User.Identity.Name;
            var response = await Mediator.Send(new GetCurrentUserQuery { UserName = userName });

            return Ok(response);
        }
        
        [HttpPost("change-password")]
        [Authorize(AuthenticationSchemes = "Admin")]
        [AuthorizeDefinition(ActionType = ActionType.Updating, Definition = "Change Password", Menu = AuthorizeDefinitionConstants.Users)]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordCommand command)
        {
            var response = await Mediator.Send(command);
            return Ok(response);
        }
        
    }
}
