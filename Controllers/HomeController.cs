using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Net.Http.Json;
using System.Reflection;
using System.Globalization;
using Task2.model;
using System.Diagnostics;

namespace YourNamespace.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FileUploadController : ControllerBase
    {
        private readonly string[] AllowedContentTypes = { "image/jpeg", "video/mp4" };

        [HttpPost("Upload")]
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
                var existingJsonContent = JsonConvert.DeserializeObject<FileContent>(System.IO.File.ReadAllText(jsonFilePath));

                if (!string.IsNullOrWhiteSpace(formData.Description))
                {
                    existingJsonContent.Description = formData.Description;
                }


                System.IO.File.WriteAllText(jsonFilePath, JsonConvert.SerializeObject(existingJsonContent));

                if (formData.File != null)
                {
                    var existingFilePath = Path.Combine("Files", $"{formData.FileName}");
                    System.IO.File.Delete(existingFilePath);

                    Update(formData.File, existingJsonContent);
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

                    //HttpContext.Response.Headers.Add("TEST", "TEST");
                    HttpContext.Response.ContentType = contentType;
                    HttpContext.Response.Headers["File-Name"] = $"{formData.FileName}.{fileInfo.Extension}";
                    HttpContext.Response.Headers["File-Owner"] = formData.Owner;
                    
                    //var desc = fileInfo.Descriptionl;

                    //Response.Headers.Add("File-Description", desc);
                    //Description still not working?




                    // Use the File method to return the file
                    return File(System.IO.File.ReadAllBytes(filePath), contentType, $"{fileInfo.FileName}.{fileInfo.Extension}");
                }
                catch (JsonException)
                {
                    return StatusCode(500, "Error deserializing metadata file.");
                }
                catch (Exception ex)
                {
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
                Description = formData.Description,
                CreationDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                ModificationDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };

            System.IO.File.WriteAllText(jsonFilePath, JsonConvert.SerializeObject(metadata));
        }

        private void Update(IFormFile file, FileContent formData)
        {
            var fileExtension = file.ContentType == "image/jpeg" ? "jpg" : "mp4";
            var filePath = Path.Combine("Files", $"{formData.FileName}.{fileExtension}");

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                file.CopyTo(stream);
            }

            var jsonFilePath = Path.Combine("Files", $"{formData.FileName}.json");

            var fileInfo = JsonConvert.DeserializeObject<FileContent>(System.IO.File.ReadAllText(jsonFilePath));

            var metadata = new
            {
                FileName = formData.FileName,
                Extension = fileExtension,
                Owner = formData.Owner,
                Description = formData.Description,
                CreationDate = formData.CreationDate,
                ModificationDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };

            System.IO.File.WriteAllText(jsonFilePath, JsonConvert.SerializeObject(metadata));
        }

        [HttpPost("Filter-By-Date")]

        public IActionResult FilterByDate([FromForm] FilterByDateFormData formData)
        {
            if (string.IsNullOrEmpty(formData.StartDate))
            {
                return BadRequest("StartDate is required for the 'Filter By Creation Date' operation.");
            }

            if (!DateTime.TryParse(formData.StartDate, out var startDate))
            {
                return BadRequest("Invalid StartDate format");
            }

            DateTime? endDate = null;
            if (string.IsNullOrEmpty(formData.EndDate))
            {
                return BadRequest("Invalid EndDate format");
            }

            if (formData.SortType == null)
            {
                formData.SortType = SortType.Ascending;
            }

            string[] jsonFiles = Directory.GetFiles("Files", "*.json");

            List<FileContent> response = new List<FileContent>();

            foreach (var jsonFilePath in jsonFiles)
            {
                var metadata = JsonConvert.DeserializeObject<FileContent>(System.IO.File.ReadAllText(jsonFilePath));

                DateTime creationDate = DateTime.Parse(metadata.CreationDate);

                if (metadata.Owner == formData.Owner && creationDate > startDate && (!endDate.HasValue || creationDate < endDate.Value))
                {
                    response.Add(new FileContent
                    {
                        FileName = metadata.FileName,
                        CreationDate = creationDate.ToString(),
                        Owner = metadata.Owner,
                    });
                }
            }

            if (formData.SortType == SortType.Ascending)
            {
                response = response.OrderBy(file => file.CreationDate).ToList();
            }
            else if (formData.SortType == SortType.Descending)
            {
                response = response.OrderByDescending(file => file.CreationDate).ToList();
            }

            return Ok(response);
        }


        [HttpPost("Filter-By-User")]

        public IActionResult FilterByUser([FromForm] FilterByUserFormData formData)
        {
            if (formData.Name == null || formData.Name.Length == 0)
            {
                return BadRequest("At least one UserName is required for the 'Filter By User' operation.");
            }

            if (string.IsNullOrEmpty(formData.StartDate))
            {
                return BadRequest("StartDate is required for the 'Filter By User' operation.");
            }

            if (!DateTime.TryParse(formData.StartDate, out var startDate))
            {
                return BadRequest("Invalid StartDate format");
            }

            DateTime? endDate = null;
            if (string.IsNullOrEmpty(formData.EndDate))
            {
                return BadRequest("Invalid EndDate format");
            }

            if (formData.SortType == null)
            {
                formData.SortType = SortType.Ascending;
            }

            string[] jsonFiles = Directory.GetFiles("Files", "*.json");

            List<FileContent> response = new List<FileContent>();

            foreach (var jsonFilePath in jsonFiles)
            {
                var metadata = JsonConvert.DeserializeObject<FileContent>(System.IO.File.ReadAllText(jsonFilePath));

                if (formData.Name.Contains(metadata.Owner))
                {
                    DateTime creationDate = DateTime.Parse(metadata.CreationDate);

                    if (creationDate > startDate && (!endDate.HasValue || creationDate < endDate.Value))
                    {
                        response.Add(new FileContent
                        {
                            FileName = metadata.FileName,
                            Owner = metadata.Owner,
                            CreationDate = creationDate.ToString(),
                            ModificationDate = metadata.ModificationDate
                        });
                    }
                }
            }

            if (formData.SortType == SortType.Ascending)
            {
                response = response.OrderBy(file => file.CreationDate).ToList();
            }
            else if (formData.SortType == SortType.Descending)
            {
                response = response.OrderByDescending(file => file.CreationDate).ToList();
            }

            return Ok(response);
        }
    }

}
