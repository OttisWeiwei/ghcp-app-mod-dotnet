using System;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using System.IO;
using System.Web;
using System.Threading.Tasks;
using ContosoUniversity.Data;
using ContosoUniversity.Models;
using ContosoUniversity.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace ContosoUniversity.Controllers
{
    public class CoursesController : BaseController
    {
        private AzureBlobStorageService _azureBlobStorageService;
        private readonly string _teachingMaterialsContainer = "teaching-materials";

        public CoursesController()
            : base()
        {
            // Initialize Azure Blob Storage Service if configuration is available
            try
            {
                // Note: In this hybrid ASP.NET MVC/Core application, we get config from environment
                var endpoint = Environment.GetEnvironmentVariable("AzureStorageBlob__Endpoint") 
                    ?? "https://yourstorageaccount.blob.core.windows.net";
                
                if (!endpoint.Contains("yourstorageaccount"))
                {
                    _azureBlobStorageService = new AzureBlobStorageService(endpoint);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to initialize Azure Blob Storage Service: {ex.Message}");
            }
        }

        // GET: Courses
        public ActionResult Index()
        {
            var courses = db.Courses.Include(c => c.Department);
            return View(courses.ToList());
        }

        // GET: Courses/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Course course = db.Courses.Include(c => c.Department).Where(c => c.CourseID == id).Single();
            if (course == null)
            {
                return HttpNotFound();
            }
            return View(course);
        }

        // GET: Courses/Create
        public ActionResult Create()
        {
            ViewBag.DepartmentID = new SelectList(db.Departments, "DepartmentID", "Name");
            return View(new Course());
        }

        // POST: Courses/Create - Now async to support async blob operations
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create([Bind(Include = "CourseID,Title,Credits,DepartmentID,TeachingMaterialImagePath")] Course course, HttpPostedFileBase teachingMaterialImage)
        {
            if (ModelState.IsValid)
            {
                // Handle file upload if an image is provided
                if (teachingMaterialImage != null && teachingMaterialImage.ContentLength > 0)
                {
                    // Validate file type
                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp" };
                    var fileExtension = Path.GetExtension(teachingMaterialImage.FileName).ToLower();
                    
                    if (!allowedExtensions.Contains(fileExtension))
                    {
                        ModelState.AddModelError("teachingMaterialImage", "Please upload a valid image file (jpg, jpeg, png, gif, bmp).");
                        ViewBag.DepartmentID = new SelectList(db.Departments, "DepartmentID", "Name", course.DepartmentID);
                        return View(course);
                    }

                    // Validate file size (max 5MB)
                    if (teachingMaterialImage.ContentLength > 5 * 1024 * 1024)
                    {
                        ModelState.AddModelError("teachingMaterialImage", "File size must be less than 5MB.");
                        ViewBag.DepartmentID = new SelectList(db.Departments, "DepartmentID", "Name", course.DepartmentID);
                        return View(course);
                    }

                    try
                    {
                        if (_azureBlobStorageService != null)
                        {
                            // Upload to Azure Blob Storage
                            var fileName = $"course_{course.CourseID}_{Guid.NewGuid()}{fileExtension}";
                            var blobUri = await _azureBlobStorageService.UploadBlobAsync(
                                _teachingMaterialsContainer, 
                                fileName, 
                                teachingMaterialImage.InputStream,
                                overwrite: true);
                            course.TeachingMaterialImagePath = blobUri;
                        }
                        else
                        {
                            // Fallback to local file system if Azure Blob Storage is not configured
                            var uploadsPath = Server.MapPath("~/Uploads/TeachingMaterials/");
                            if (!Directory.Exists(uploadsPath))
                            {
                                Directory.CreateDirectory(uploadsPath);
                            }
                            var fileName = $"course_{course.CourseID}_{Guid.NewGuid()}{fileExtension}";
                            var filePath = Path.Combine(uploadsPath, fileName);
                            teachingMaterialImage.SaveAs(filePath);
                            course.TeachingMaterialImagePath = $"~/Uploads/TeachingMaterials/{fileName}";
                        }
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddModelError("teachingMaterialImage", "Error uploading file: " + ex.Message);
                        ViewBag.DepartmentID = new SelectList(db.Departments, "DepartmentID", "Name", course.DepartmentID);
                        return View(course);
                    }
                }

                db.Courses.Add(course);
                db.SaveChanges();
                
                // Send notification for course creation
                SendEntityNotification("Course", course.CourseID.ToString(), course.Title, EntityOperation.CREATE);
                
                return RedirectToAction("Index");
            }

            ViewBag.DepartmentID = new SelectList(db.Departments, "DepartmentID", "Name", course.DepartmentID);
            return View(course);
        }

        // GET: Courses/Edit/5
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Course course = db.Courses.Find(id);
            if (course == null)
            {
                return HttpNotFound();
            }
            ViewBag.DepartmentID = new SelectList(db.Departments, "DepartmentID", "Name", course.DepartmentID);
            return View(course);
        }

        // POST: Courses/Edit/5 - Now async to support async blob operations
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit([Bind(Include = "CourseID,Title,Credits,DepartmentID,TeachingMaterialImagePath")] Course course, HttpPostedFileBase teachingMaterialImage)
        {
            if (ModelState.IsValid)
            {
                // Handle file upload if a new image is provided
                if (teachingMaterialImage != null && teachingMaterialImage.ContentLength > 0)
                {
                    // Validate file type
                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp" };
                    var fileExtension = Path.GetExtension(teachingMaterialImage.FileName).ToLower();
                    
                    if (!allowedExtensions.Contains(fileExtension))
                    {
                        ModelState.AddModelError("teachingMaterialImage", "Please upload a valid image file (jpg, jpeg, png, gif, bmp).");
                        ViewBag.DepartmentID = new SelectList(db.Departments, "DepartmentID", "Name", course.DepartmentID);
                        return View(course);
                    }

                    // Validate file size (max 5MB)
                    if (teachingMaterialImage.ContentLength > 5 * 1024 * 1024)
                    {
                        ModelState.AddModelError("teachingMaterialImage", "File size must be less than 5MB.");
                        ViewBag.DepartmentID = new SelectList(db.Departments, "DepartmentID", "Name", course.DepartmentID);
                        return View(course);
                    }

                    try
                    {
                        if (_azureBlobStorageService != null)
                        {
                            // Delete old blob if exists
                            if (!string.IsNullOrEmpty(course.TeachingMaterialImagePath))
                            {
                                try
                                {
                                    // Extract filename from blob URI
                                    var oldBlobName = new Uri(course.TeachingMaterialImagePath).Segments.Last();
                                    await _azureBlobStorageService.DeleteBlobAsync(_teachingMaterialsContainer, oldBlobName);
                                }
                                catch (Exception ex)
                                {
                                    // Log but don't fail if old blob can't be deleted
                                    System.Diagnostics.Debug.WriteLine($"Error deleting old blob: {ex.Message}");
                                }
                            }

                            // Upload new blob
                            var fileName = $"course_{course.CourseID}_{Guid.NewGuid()}{fileExtension}";
                            var blobUri = await _azureBlobStorageService.UploadBlobAsync(
                                _teachingMaterialsContainer, 
                                fileName, 
                                teachingMaterialImage.InputStream,
                                overwrite: true);
                            course.TeachingMaterialImagePath = blobUri;
                        }
                        else
                        {
                            // Fallback to local file system if Azure Blob Storage is not configured
                            var uploadsPath = Server.MapPath("~/Uploads/TeachingMaterials/");
                            if (!Directory.Exists(uploadsPath))
                            {
                                Directory.CreateDirectory(uploadsPath);
                            }

                            var fileName = $"course_{course.CourseID}_{Guid.NewGuid()}{fileExtension}";
                            var filePath = Path.Combine(uploadsPath, fileName);

                            // Delete old file if exists
                            if (!string.IsNullOrEmpty(course.TeachingMaterialImagePath) && course.TeachingMaterialImagePath.StartsWith("~/"))
                            {
                                var oldFilePath = Server.MapPath(course.TeachingMaterialImagePath);
                                if (System.IO.File.Exists(oldFilePath))
                                {
                                    System.IO.File.Delete(oldFilePath);
                                }
                            }

                            // Save new file
                            teachingMaterialImage.SaveAs(filePath);
                            course.TeachingMaterialImagePath = $"~/Uploads/TeachingMaterials/{fileName}";
                        }
                    }
                    catch (Exception ex)
                    {
                        ModelState.AddModelError("teachingMaterialImage", "Error uploading file: " + ex.Message);
                        ViewBag.DepartmentID = new SelectList(db.Departments, "DepartmentID", "Name", course.DepartmentID);
                        return View(course);
                    }
                }

                db.Entry(course).State = EntityState.Modified;
                db.SaveChanges();
                
                // Send notification for course update
                SendEntityNotification("Course", course.CourseID.ToString(), course.Title, EntityOperation.UPDATE);
                
                return RedirectToAction("Index");
            }
            ViewBag.DepartmentID = new SelectList(db.Departments, "DepartmentID", "Name", course.DepartmentID);
            return View(course);
        }

        // GET: Courses/Delete/5
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Course course = db.Courses.Include(c => c.Department).Where(c => c.CourseID == id).Single();
            if (course == null)
            {
                return HttpNotFound();
            }
            return View(course);
        }

        // POST: Courses/Delete/5 - Now async to support async blob operations
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteConfirmed(int id)
        {
            Course course = db.Courses.Find(id);
            var courseTitle = course.Title;
            
            // Delete associated blob or file
            if (!string.IsNullOrEmpty(course.TeachingMaterialImagePath))
            {
                try
                {
                    if (_azureBlobStorageService != null && course.TeachingMaterialImagePath.StartsWith("https://"))
                    {
                        // Delete from Azure Blob Storage
                        var blobName = new Uri(course.TeachingMaterialImagePath).Segments.Last();
                        await _azureBlobStorageService.DeleteBlobAsync(_teachingMaterialsContainer, blobName);
                    }
                    else if (course.TeachingMaterialImagePath.StartsWith("~/"))
                    {
                        // Delete from local file system
                        var filePath = Server.MapPath(course.TeachingMaterialImagePath);
                        if (System.IO.File.Exists(filePath))
                        {
                            System.IO.File.Delete(filePath);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Log the error but don't prevent deletion of the course
                    System.Diagnostics.Debug.WriteLine($"Error deleting file/blob: {ex.Message}");
                }
            }
            
            db.Courses.Remove(course);
            db.SaveChanges();
            
            // Send notification for course deletion
            SendEntityNotification("Course", id.ToString(), courseTitle, EntityOperation.DELETE);
            
            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Base class will dispose db and notificationService
            }
            base.Dispose(disposing);
        }
    }
}
