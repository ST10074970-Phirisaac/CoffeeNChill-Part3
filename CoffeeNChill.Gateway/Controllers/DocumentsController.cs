using System.Net;
using CoffeeNChill.Gateway.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CoffeeNChill.Gateway.Controllers;

[ApiController]
[Route("api/documents")]
[Authorize(Roles = "Admin,Staff")]
public class DocumentsController : ControllerBase
{
    private readonly IHttpClientFactory _httpClientFactory;

    public DocumentsController(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListDocuments()
    {
        var client = _httpClientFactory.CreateClient("AzureFunctions");

        try
        {
            using var response = await client.GetAsync(
                "api/documents",
                HttpContext.RequestAborted);

            return await ForwardResponseAsync(response);
        }
        catch (HttpRequestException)
        {
            return BadGateway();
        }
        catch (OperationCanceledException)
            when (!HttpContext.RequestAborted.IsCancellationRequested)
        {
            return GatewayTimeout();
        }
    }

    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UploadDocument(IFormFile file)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new ErrorResponseDto
            {
                Message = "Please select a file."
            });
        }

        if (file.Length > 10 * 1024 * 1024)
        {
            return BadRequest(new ErrorResponseDto
            {
                Message = "File must not exceed 10 MB."
            });
        }

        var client = _httpClientFactory.CreateClient("AzureFunctions");
        using var form = new MultipartFormDataContent();
        await using var stream = file.OpenReadStream();
        using var fileContent = new StreamContent(stream);

        if (!string.IsNullOrWhiteSpace(file.ContentType))
        {
            fileContent.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue(file.ContentType);
        }

        form.Add(fileContent, "file", Path.GetFileName(file.FileName));

        try
        {
            using var response = await client.PostAsync(
                "api/documents/upload",
                form,
                HttpContext.RequestAborted);

            return await ForwardResponseAsync(response);
        }
        catch (HttpRequestException)
        {
            return BadGateway();
        }
        catch (OperationCanceledException)
            when (!HttpContext.RequestAborted.IsCancellationRequested)
        {
            return GatewayTimeout();
        }
    }

    [HttpGet("download/{fileName}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadDocument(string fileName)
    {
        var client = _httpClientFactory.CreateClient("AzureFunctions");

        try
        {
            using var response = await client.GetAsync(
                "api/documents/download/" + Uri.EscapeDataString(fileName),
                HttpContext.RequestAborted);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return NotFound(new ErrorResponseDto
                {
                    Message = "Document not found."
                });
            }

            if (!response.IsSuccessStatusCode)
            {
                return await ForwardResponseAsync(response);
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(
                HttpContext.RequestAborted);

            return File(
                bytes,
                "application/octet-stream",
                Path.GetFileName(fileName));
        }
        catch (HttpRequestException)
        {
            return BadGateway();
        }
        catch (OperationCanceledException)
            when (!HttpContext.RequestAborted.IsCancellationRequested)
        {
            return GatewayTimeout();
        }
    }

    [HttpDelete("{fileName}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ErrorResponseDto), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteDocument(string fileName)
    {
        var client = _httpClientFactory.CreateClient("AzureFunctions");

        try
        {
            using var response = await client.DeleteAsync(
                "api/documents/" + Uri.EscapeDataString(fileName),
                HttpContext.RequestAborted);

            if (response.StatusCode == HttpStatusCode.NoContent)
            {
                return NoContent();
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return NotFound(new ErrorResponseDto
                {
                    Message = "Document not found."
                });
            }

            return await ForwardResponseAsync(response);
        }
        catch (HttpRequestException)
        {
            return BadGateway();
        }
        catch (OperationCanceledException)
            when (!HttpContext.RequestAborted.IsCancellationRequested)
        {
            return GatewayTimeout();
        }
    }

    private async Task<IActionResult> ForwardResponseAsync(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync(
            HttpContext.RequestAborted);

        return new ContentResult
        {
            Content = content,
            ContentType = response.Content.Headers.ContentType?.ToString()
                          ?? "text/plain",
            StatusCode = (int)response.StatusCode
        };
    }

    private IActionResult BadGateway() =>
        StatusCode(502, new ErrorResponseDto
        {
            Message = "Azure Functions service is unavailable."
        });

    private IActionResult GatewayTimeout() =>
        StatusCode(504, new ErrorResponseDto
        {
            Message = "Azure Functions service timed out."
        });
}
