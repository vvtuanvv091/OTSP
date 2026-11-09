// using System.Security.Claims;
// using Microsoft.AspNetCore.Http;
// using Microsoft.AspNetCore.Mvc;
// using Microsoft.Extensions.Caching.Memory;
// using Moq;
// using Backend_Project_OTSP.Controllers.Tutor;
// using Backend_Project_OTSP.Data;
// using Backend_Project_OTSP.Dtos.Tutor;
// using Backend_Project_OTSP.Models;
// using Backend_Project_OTSP.Services.Common;
// using Backend_Project_OTSP.tests.Helpers;
// using Xunit;
// using static Backend_Project_OTSP.Models.Enums;

// namespace Backend_Project_OTSP.tests.Controllers.Tutor;

// public class TutorProfileApiControllerTests
// {
//     private readonly Mock<IImageStorageService> _mockImageStorage;
//     private readonly IMemoryCache _memoryCache;

//     public TutorProfileApiControllerTests()
//     {
//         _mockImageStorage = new Mock<IImageStorageService>();
//         _memoryCache = new MemoryCache(new MemoryCacheOptions());
//     }

//     private TutorProfileApiController CreateController(ApplicationDbContext db, string? userId = null)
//     {
//         var controller = new TutorProfileApiController(db, _memoryCache, _mockImageStorage.Object);
//         if (!string.IsNullOrEmpty(userId))
//         {
//             var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
//             {
//                 new Claim(ClaimTypes.NameIdentifier, userId),
//                 new Claim(ClaimTypes.Role, "Tutor")
//             }, "TestAuth"));

//             controller.ControllerContext = new ControllerContext
//             {
//                 HttpContext = new DefaultHttpContext { User = user }
//             };
//         }
//         return controller;
//     }

//     [Fact]
//     public async Task GetProfile_WhenTutorExists_ReturnsOkResultWithProfile()
//     {
//         var db = TestDbContextFactory.Create();
//         var user = new ApplicationUser
//         {
//             Id = "tutor-1",
//             UserName = "tutor1@test.com",
//             Email = "tutor1@test.com",
//             FullName = "Gia Sư 1"
//         };
//         var tutorProfile = new TutorProfile
//         {
//             UserId = user.Id,
//             User = user,
//             HourlyFee = 250000,
//             Introduction = "Kinh nghiệm 5 năm dạy kèm",
//             VerificationStatus = VerificationStatus.Approved
//         };
//         db.Users.Add(user);
//         db.TutorProfiles.Add(tutorProfile);
//         await db.SaveChangesAsync();

//         var controller = CreateController(db, user.Id);

//         var result = await controller.GetProfile(CancellationToken.None);

//         var okResult = Assert.IsType<OkObjectResult>(result);
//         Assert.NotNull(okResult.Value);
//     }

//     [Fact]
//     public async Task UpdateProfile_WhenValid_UpdatesInformation()
//     {
//         var db = TestDbContextFactory.Create();
//         var user = new ApplicationUser
//         {
//             Id = "tutor-2",
//             UserName = "tutor2@test.com",
//             FullName = "Tên Cũ"
//         };
//         var tutorProfile = new TutorProfile
//         {
//             UserId = user.Id,
//             User = user,
//             HourlyFee = 150000
//         };
//         db.Users.Add(user);
//         db.TutorProfiles.Add(tutorProfile);
//         await db.SaveChangesAsync();

//         var controller = CreateController(db, user.Id);
//         var request = new UpdateTutorProfileRequest
//         {
//             DisplayName = "Tên Mới Gia Sư",
//             Introduction = "Giới thiệu mới",
//             HourlyFee = 200000,
//             CitizenIdNumber = "123456789012"
//         };

//         var result = await controller.UpdateProfile(request, CancellationToken.None);

//         var okResult = Assert.IsType<OkObjectResult>(result);
//         var updatedProfile = await db.TutorProfiles.FindAsync(tutorProfile.Id);
//         Assert.Equal(200000, updatedProfile!.HourlyFee);
//         Assert.Equal("Giới thiệu mới", updatedProfile.Introduction);
//     }
// }
