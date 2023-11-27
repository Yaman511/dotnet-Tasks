using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Net.Http.Json;
using System.Reflection;
using Task2.model;

namespace YourNamespace.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FileUploadController : ControllerBase
    {
        private readonly string[] AllowedContentTypes = { "image/jpeg", "video/mp4" };

        [HttpPost("upload")]
        public IActionResult UploadFile([FromForm] FormData formData)
        {

            try
            {
                switch (formData.QueryType)
                {
                    case QueryType.Create:
                        return CreateFile(formData);

                    case QueryType.Update:
                        return UpdateFile(formData);

                    case QueryType.Delete:
                        return DeleteFile(formData);

                    case QueryType.Retrieve:
                        return RetrieveFile(formData);

                    default:
                        return BadRequest("Invalid operation type. Please provide a valid type (0, 1, 2, or 3).");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing upload: {ex.Message}");
                return StatusCode(500, "Internal Server Error");
            }
        }

        private IActionResult CreateFile(FormData formData)
        {
            if (string.IsNullOrWhiteSpace(formData.FileName) ||
                string.IsNullOrWhiteSpace(formData.Owner) ||
                formData.File == null)
            {
                return BadRequest("All form fields are required for the 'Create' operation.");
            }

            if (!IsValidFile(formData.File))
            {
                return BadRequest("File type not supported. Please upload a jpg or mp4 file.");
            }

            if (FileExists(formData.FileName, formData.File.ContentType == "image/jpeg" ? "jpg" : "mp4"))
            {
                return Conflict("File with the same name already exists.");
            }

            else
            {

                SaveFile(formData.File, formData);

                return CreatedAtAction(nameof(UploadFile), new { fileName = formData.FileName }, "File and metadata created successfully.");
            }
        }

        private IActionResult UpdateFile(FormData formData)
        {
            if (string.IsNullOrWhiteSpace(formData.FileName) ||
                string.IsNullOrWhiteSpace(formData.Owner))
            {
                return BadRequest("Filename and owner name are required for the 'Update' operation.");
            }

            if (!FileExists(formData.FileName, formData.File.ContentType == "image/jpeg" ? "jpg" : "mp4"))
            {
                return NotFound("File does not exist.");
            }

            if (!IsValidOwner(formData.FileName, formData.Owner))
            {
                return Unauthorized("Invalid owner for the specified file.");
            }

            if (formData.File != null || !string.IsNullOrWhiteSpace(formData.Description))
            {
                var jsonFilePath = Path.Combine("Files", $"{formData.FileName}.json");
                var existingJsonContent = JsonConvert.DeserializeObject<FormData>(System.IO.File.ReadAllText(jsonFilePath));

                if (!string.IsNullOrWhiteSpace(formData.Description))
                {
                    existingJsonContent.Description = formData.Description;
                }

                System.IO.File.WriteAllText(jsonFilePath, JsonConvert.SerializeObject(existingJsonContent));

                if (formData.File != null)
                {
                    var existingFilePath = Path.Combine("Files", $"{formData.FileName}");
                    System.IO.File.Delete(existingFilePath);

                    SaveFile(formData.File, existingJsonContent);
                }

                return Ok("File and metadata updated successfully.");
            }
            else
            {
                return BadRequest("Either file content or description should be provided for the 'Update' operation.");
            }
        }

        private IActionResult DeleteFile(FormData formData)
        {
            if (string.IsNullOrWhiteSpace(formData.FileName) ||
                string.IsNullOrWhiteSpace(formData.Owner))
            {
                return BadRequest("Filename and owner name are required for the 'Delete' operation.");
            }

            var jsonFilePath = Path.Combine("Files", $"{formData.FileName}.json");

            if (!System.IO.File.Exists(jsonFilePath))
            {
                return NotFound("File does not exist.");
            }

            var fileInfo = JsonConvert.DeserializeObject<dynamic>(System.IO.File.ReadAllText(jsonFilePath));

            if (!IsValidOwner(formData.FileName, formData.Owner))
            {
                return Unauthorized("Invalid owner for the specified file.");
            }

            var filePath = Path.Combine("Files", $"{formData.FileName}.{fileInfo.Extension}");

            System.IO.File.Delete(filePath);
            System.IO.File.Delete(jsonFilePath);

            return Ok("File and metadata deleted successfully.");
        }

        private IActionResult RetrieveFile(FormData formData)
        {
            if (string.IsNullOrWhiteSpace(formData.FileName) ||
                string.IsNullOrWhiteSpace(formData.Owner))
            {
                return BadRequest("Filename and owner name are required for the 'Retrieve' operation.");
            }

            var jsonFilePath = Path.Combine("Files", $"{formData.FileName}.json");

            if (!System.IO.File.Exists(jsonFilePath))
            {
                return NotFound("File does not exist.");
            }
            else
            {
                try
                {
                    var fileInfo = JsonConvert.DeserializeObject<dynamic>(System.IO.File.ReadAllText(jsonFilePath));
                    //var hInfo = JsonConvert.DeserializeObject<FormData>(System.IO.File.ReadAllText(jsonFilePath));
                    if (!IsValidOwner(formData.FileName, formData.Owner))
                    {
                        return Unauthorized("Invalid owner for the specified file.");
                    }

                    var filePath = Path.Combine("Files", $"{formData.FileName}.{fileInfo.Extension}");
                    string contentType = "image/jpeg";
                    if (fileInfo.Extension != "jpg")
                    {
                        contentType = "video/mp4";
                    }


                    //Response.Headers.Add("File Name", $"{formData.FileName}.{fileInfo.Extension}");
                    //Response.Headers.Add("File-Owner", formData.Owner);
                    //Response.Headers.Add("File-Description", fileInfo.Description);


                    // Use the File method to return the file
                    return File(System.IO.File.ReadAllBytes(filePath), contentType, $"{fileInfo.FileName}.{fileInfo.Extension}");
                }
                catch (JsonException)
                {
                    // Handle JSON deserialization errors
                    return StatusCode(500, "Error deserializing metadata file.");
                }
                catch (Exception ex)
                {
                    // Handle other exceptions
                    Console.WriteLine($"Error retrieving file: {ex.Message}");
                    return StatusCode(500, "Internal Server Error");
                }
            }

            

        }



        private bool FileExists(string fileName, string fileExtension)
        {

            var filePath = Path.Combine("Files", $"{fileName}.{fileExtension}");
            return System.IO.File.Exists(filePath);
        }

        private bool IsValidOwner(string fileName, string owner)
        {
            var jsonFilePath = Path.Combine("Files", $"{fileName}.json");

            if (System.IO.File.Exists(jsonFilePath))
            {
                var fileInfo = JsonConvert.DeserializeObject<FormData>(System.IO.File.ReadAllText(jsonFilePath));
                return fileInfo != null && fileInfo.Owner == owner;
            }

            return false;
        }

        private bool IsValidFile(IFormFile? file)
        {
            return file != null && AllowedContentTypes.Contains(file.ContentType);
        }

        private void SaveFile(IFormFile file, FormData formData)
        {
            var fileExtension = file.ContentType == "image/jpeg" ? "jpg" : "mp4";
            var filePath = Path.Combine("Files", $"{formData.FileName}.{fileExtension}");

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                file.CopyTo(stream);
            }

            var jsonFilePath = Path.Combine("Files", $"{formData.FileName}.json");

            var metadata = new
            {
                FileName = formData.FileName,
                Extension = fileExtension,
                Owner = formData.Owner,
                Description = formData.Description
            };

            System.IO.File.WriteAllText(jsonFilePath, JsonConvert.SerializeObject(metadata));
        }

        [HttpPost("filter")]

        public IActionResult Save()
        {
            return Ok();
        }
    }

}
