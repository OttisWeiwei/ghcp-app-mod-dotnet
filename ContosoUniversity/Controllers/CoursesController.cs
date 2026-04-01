using System;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.IO;
using System.Threading.Tasks;
using ContosoUniversity.Data;
using ContosoUniversity.Models;
using ContosoUniversity.Services;
using Microsoft.AspNetCore.Http;

namespace ContosoUniversity.Controllers
{
    public class CoursesController : BaseController
    {
        private AzureBlobStorageService _azureBlobStorageService;
        private readonly string _teachingMaterialsContainer = "teaching-materials";

        public CoursesController(SchoolContext context, AzureBlobStorageService azureBlobStorageService = null)
            : base(context)
        {
            _azureBlobStorageService = azureBlobStorageService;
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
                return BadRequest();
            }
            Course course = db.Courses.Include(c => c.Department).Where(c => c.CourseID == id).Single();
            if (course == null)
            {
                return NotFound();
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
        public async Task<ActionResult> Create([Bind("CourseID,Title,Credits,DepartmentID,TeachingMaterialImagePath")] Course course, IFormFile teachingMaterialImage)
        {
            if (ModelState.IsValid)
            {
                // Handle file upload if an image is provided
                if (teachingMaterialImage != null && teachingMaterialImage.Length > 0)
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
                    if (teachingMaterialImage.Length > 5 * 1024 * 1024)
                    {
                        ModelState.AddModelError("teachingMaterialImage", "File size must be less than 5MB.");
                        ViewBag.DepartmentID = new SelectList(db.Departments, "DepartmentID", "Name", course.DepartmentID);
                        return View(course);
                    }

                    try
                    {
                        var fileName = $"course_{course.CourseID}_{Guid.NewGuid()}{fileExtension}";
                        var blobUri = await _azureBlobStorageService.UploadBlobAsync(
                            _teachingMaterialsContainer,
                            fileName,
                            teachingMaterialImage.OpenReadStream(),
                            overwrite: true);
                        course.TeachingMaterialImagePath = blobUri;
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
                return BadRequest();
            }
            Course course = db.Courses.Find(id);
            if (course == null)
            {
                return NotFound();
            }
            ViewBag.DepartmentID = new SelectList(db.Departments, "DepartmentID", "Name", course.DepartmentID);
            return View(course);
        }

        // POST: Courses/Edit/5 - Now async to support async blob operations
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit([Bind("CourseID,Title,Credits,DepartmentID,TeachingMaterialImagePath")] Course course, IFormFile teachingMaterialImage)
        {
            if (ModelState.IsValid)
            {
                // Handle file upload if a new image is provided
                if (teachingMaterialImage != null && teachingMaterialImage.Length > 0)
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
                    if (teachingMaterialImage.Length > 5 * 1024 * 1024)
                    {
                        ModelState.AddModelError("teachingMaterialImage", "File size must be less than 5MB.");
                        ViewBag.DepartmentID = new SelectList(db.Departments, "DepartmentID", "Name", course.DepartmentID);
                        return View(course);
                    }

                    try
                    {
                        // Delete old blob if exists
                        if (!string.IsNullOrEmpty(course.TeachingMaterialImagePath))
                        {
                            try
                            {
                                var oldBlobName = new Uri(course.TeachingMaterialImagePath).Segments.Last();
                                await _azureBlobStorageService.DeleteBlobAsync(_teachingMaterialsContainer, oldBlobName);
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"Error deleting old blob: {ex.Message}");
                            }
                        }

                        var fileName = $"course_{course.CourseID}_{Guid.NewGuid()}{fileExtension}";
                        var blobUri = await _azureBlobStorageService.UploadBlobAsync(
                            _teachingMaterialsContainer,
                            fileName,
                            teachingMaterialImage.OpenReadStream(),
                            overwrite: true);
                        course.TeachingMaterialImagePath = blobUri;
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
                return BadRequest();
            }
            Course course = db.Courses.Include(c => c.Department).Where(c => c.CourseID == id).Single();
            if (course == null)
            {
                return NotFound();
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

            // Delete associated blob
            if (!string.IsNullOrEmpty(course.TeachingMaterialImagePath))
            {
                try
                {
                    if (_azureBlobStorageService != null && course.TeachingMaterialImagePath.StartsWith("https://"))
                    {
                        var blobName = new Uri(course.TeachingMaterialImagePath).Segments.Last();
                        await _azureBlobStorageService.DeleteBlobAsync(_teachingMaterialsContainer, blobName);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error deleting blob: {ex.Message}");
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
