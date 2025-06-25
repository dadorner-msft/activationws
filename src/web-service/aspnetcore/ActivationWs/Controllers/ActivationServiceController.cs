using ActivationWs.Services;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Text.RegularExpressions;
using static System.Net.WebRequestMethods;

namespace ActivationWs.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ActivationServiceController : ControllerBase {
        private readonly ActivationProcessor _activationManager;

        public ActivationServiceController(ActivationProcessor activationManager) {
            _activationManager = activationManager;
        }

        // Endpoint to retrieve a ConfirmationId
        [HttpGet("ConfirmationId")]
        public async Task<ActionResult<object>> GetConfirmationId(
            [FromQuery] string hostName,
            [FromQuery] string installationId,
            [FromQuery] string extendedProductId)
        {
            try {
                var (confirmationId, cached) = await _activationManager.GetConfirmationIdAsync(hostName, installationId, extendedProductId);
                return Ok(new { confirmationId, cached });

            } catch (ArgumentException argEx) {
                return Problem(
                    title: "Invalid input.",
                    detail: argEx.Message,
                    statusCode: StatusCodes.Status400BadRequest
                );

            } catch (HttpRequestException httpEx) {
                return Problem(
                    title: "External service error.",
                    detail: httpEx.Message,
                    statusCode: StatusCodes.Status503ServiceUnavailable
                );

            } catch (Exception ex) {
                return Problem(
                    title: "An unexpected error occurred.",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status500InternalServerError
                );
            }
        }

        // Endpoint to retrieve the remaining activation count
        [HttpGet("ActivationCount")]
        public async Task<ActionResult<object>> GetRemainingActivationCount(
            [FromQuery] string extendedProductId)
        {
            try {
                var result = await _activationManager.GetRemainingActivationCountAsync(extendedProductId);
                return Ok(new { remainingActivationCount = result });

            } catch (ArgumentException argEx) {
                return Problem(
                    title: "Invalid input.",
                    detail: argEx.Message,
                    statusCode: StatusCodes.Status400BadRequest
                );

            } catch (HttpRequestException httpEx) {
                return Problem(
                    title: "External service error.",
                    detail: httpEx.Message,
                    statusCode: StatusCodes.Status503ServiceUnavailable
                );

            } catch (Exception ex) {
                return Problem(
                    title: "An unexpected error occurred.",
                    detail: ex.Message,
                    statusCode: StatusCodes.Status500InternalServerError
                );
            }
        }
    }
}
