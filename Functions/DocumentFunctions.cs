
using CoffeeNChill.Functions.Services;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Net;

namespace CoffeeNChill.Functions.Functions;

public class DocumentFunctions
{
    private readonly DocumentFileService _documentFileService;

    public DocumentFunctions(DocumentFileService documentFileService)
    {
        _documentFileService = documentFileService;
    }

    // POST /api/documents/upload
    [Function("UploadStaffDocument")]
    public async Task<HttpResponseData> UploadStaffDocument(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "post",
            Route = "documents/upload")]
        HttpRequestData req)
    {
        try
        {
            if (!req.Headers.TryGetValues(
                    "Content-Type",
                    out var contentTypeValues))
            {
                var response = req.CreateResponse(
                    HttpStatusCode.BadRequest);

                await response.WriteStringAsync(
                    "Content-Type header is required.");

                return response;
            }

            var contentType = contentTypeValues.FirstOrDefault();

            if (string.IsNullOrWhiteSpace(contentType))
            {
                var response = req.CreateResponse(
                    HttpStatusCode.BadRequest);

                await response.WriteStringAsync(
                    "Content-Type header is required.");

                return response;
            }

            if (!MediaTypeHeaderValue.TryParse(
                    contentType,
                    out var mediaType))
            {
                var response = req.CreateResponse(
                    HttpStatusCode.BadRequest);

                await response.WriteStringAsync(
                    "Invalid Content-Type header.");

                return response;
            }

            if (string.IsNullOrEmpty(mediaType.MediaType.Value) ||
                !mediaType.MediaType.Value.Equals(
                    "multipart/form-data",
                    StringComparison.OrdinalIgnoreCase))
            {
                var response = req.CreateResponse(
                    HttpStatusCode.BadRequest);

                await response.WriteStringAsync(
                    "Upload must use multipart/form-data.");

                return response;
            }

            var boundarySegment =
                HeaderUtilities.RemoveQuotes(mediaType.Boundary);

            var boundary = boundarySegment.ToString();

            if (string.IsNullOrWhiteSpace(boundary))
            {
                var response = req.CreateResponse(
                    HttpStatusCode.BadRequest);

                await response.WriteStringAsync(
                    "Multipart boundary is missing.");

                return response;
            }

            var reader = new MultipartReader(
                boundary,
                req.Body);

            MultipartSection? section;

            while ((section = await reader.ReadNextSectionAsync()) != null)
            {
                if (section.Headers == null ||
                    !section.Headers.TryGetValue(
                        HeaderNames.ContentDisposition,
                        out var dispositionValues))
                {
                    continue;
                }

                var dispositionText =
                    dispositionValues.FirstOrDefault();

                if (string.IsNullOrWhiteSpace(dispositionText))
                {
                    continue;
                }

                if (!ContentDispositionHeaderValue.TryParse(
                        dispositionText,
                        out var disposition))
                {
                    continue;
                }

                if (disposition == null)
                {
                    continue;
                }

                if (!disposition.DispositionType.Equals(
                        "form-data",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var fieldName =
                    disposition.Name.ToString().Trim('"');

                if (!string.Equals(
                        fieldName,
                        "file",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var fileName =
                    disposition.FileName.ToString();

                if (string.IsNullOrWhiteSpace(fileName))
                {
                    fileName =
                        disposition.FileNameStar.ToString();
                }

                fileName = Path.GetFileName(
                    fileName.Trim('"'));

                if (string.IsNullOrWhiteSpace(fileName))
                {
                    var response = req.CreateResponse(
                        HttpStatusCode.BadRequest);

                    await response.WriteStringAsync(
                        "File name is required.");

                    return response;
                }

                await _documentFileService.UploadAsync(
                    fileName,
                    section.Body);

                var successResponse = req.CreateResponse(
                    HttpStatusCode.Created);

                await successResponse.WriteStringAsync(
                    $"File '{fileName}' uploaded successfully.");

                return successResponse;
            }

            var noFileResponse = req.CreateResponse(
                HttpStatusCode.BadRequest);

            await noFileResponse.WriteStringAsync(
                "No file was uploaded. Use form-data field named 'file'.");

            return noFileResponse;
        }
        catch (Exception)
        {
            var response = req.CreateResponse(
                HttpStatusCode.InternalServerError);

            await response.WriteStringAsync(
                "Error uploading document.");

            return response;
        }
    }

    // GET /api/documents
    [Function("ListStaffDocuments")]
    public async Task<HttpResponseData> ListStaffDocuments(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "get",
            Route = "documents")]
        HttpRequestData req)
    {
        try
        {
            var files =
                await _documentFileService.ListAsync();

            var response = req.CreateResponse(
                HttpStatusCode.OK);

            await response.WriteAsJsonAsync(files);

            return response;
        }
        catch (Exception)
        {
            var response = req.CreateResponse(
                HttpStatusCode.InternalServerError);

            await response.WriteStringAsync(
                "Error retrieving documents.");

            return response;
        }
    }

    // GET /api/documents/download/{fileName}
    [Function("DownloadStaffDocument")]
    public async Task<HttpResponseData> DownloadStaffDocument(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "get",
            Route = "documents/download/{fileName}")]
        HttpRequestData req,
        string fileName)
    {
        try
        {
            fileName = Path.GetFileName(fileName);

            if (string.IsNullOrWhiteSpace(fileName))
            {
                return req.CreateResponse(
                    HttpStatusCode.BadRequest);
            }

            using var stream =
                await _documentFileService.DownloadAsync(fileName);

            var response = req.CreateResponse(
                HttpStatusCode.OK);

            response.Headers.Add(
                "Content-Disposition",
                $"attachment; filename=\"{fileName}\"");

            response.Headers.Add(
                "Content-Type",
                "application/octet-stream");

            await stream.CopyToAsync(response.Body);

            return response;
        }
        catch (Azure.RequestFailedException ex)
            when (ex.Status == 404)
        {
            var response = req.CreateResponse(
                HttpStatusCode.NotFound);

            await response.WriteStringAsync(
                "Document not found.");

            return response;
        }
        catch (Exception)
        {
            var response = req.CreateResponse(
                HttpStatusCode.InternalServerError);

            await response.WriteStringAsync(
                "Error downloading document.");

            return response;
        }
    }

    // DELETE /api/documents/{fileName}
    // Gateway restricts this endpoint to Admin and Staff.
    [Function("DeleteStaffDocument")]
    public async Task<HttpResponseData> DeleteStaffDocument(
        [HttpTrigger(
            AuthorizationLevel.Anonymous,
            "delete",
            Route = "documents/{fileName}")]
        HttpRequestData req,
        string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) ||
            fileName == "." ||
            fileName == ".." ||
            Path.GetFileName(fileName) != fileName)
        {
            var badRequest = req.CreateResponse(
                HttpStatusCode.BadRequest);

            await badRequest.WriteStringAsync(
                "A valid file name is required.");

            return badRequest;
        }

        try
        {
            var deleted =
                await _documentFileService.DeleteAsync(fileName);

            if (!deleted)
            {
                var notFound = req.CreateResponse(
                    HttpStatusCode.NotFound);

                await notFound.WriteStringAsync(
                    "Document not found.");

                return notFound;
            }

            var response = req.CreateResponse(
                HttpStatusCode.OK);

            await response.WriteStringAsync(
                $"Document '{fileName}' deleted successfully.");

            return response;
        }
        catch (Exception)
        {
            var response = req.CreateResponse(
                HttpStatusCode.InternalServerError);

            await response.WriteStringAsync(
                "Error deleting document.");

            return response;
        }
    }
}
