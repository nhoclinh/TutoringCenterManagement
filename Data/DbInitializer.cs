using TutoringCenterManagement.Data.Entities;
using TutoringCenterManagement.Data.Enums;

namespace TutoringCenterManagement.Data
{
    /// <summary>
    /// Seed dữ liệu mẫu vào database
    /// </summary>
    public static class DbInitializer
    {
        public static void Initialize(ApplicationDbContext context)
        {
            // Kiểm tra đã có data chưa
            if (context.Accounts.Any()) return;

            // =============================================
            // SEED SHIFTS (6 ca học cố định)
            // =============================================
            var shifts = new List<Shift>
            {
                new Shift { ShiftId = ShiftId.Shift1, ShiftName = "Ca 1",
                    StartTime = new TimeOnly(8,  30), EndTime = new TimeOnly(10,  0), Description = "Sáng sớm"  },
                new Shift { ShiftId = ShiftId.Shift2, ShiftName = "Ca 2",
                    StartTime = new TimeOnly(10, 15), EndTime = new TimeOnly(11, 45), Description = "Sáng muộn" },
                new Shift { ShiftId = ShiftId.Shift3, ShiftName = "Ca 3",
                    StartTime = new TimeOnly(14, 15), EndTime = new TimeOnly(15, 45), Description = "Chiều sớm" },
                new Shift { ShiftId = ShiftId.Shift4, ShiftName = "Ca 4",
                    StartTime = new TimeOnly(16,  0), EndTime = new TimeOnly(17, 30), Description = "Chiều muộn"},
                new Shift { ShiftId = ShiftId.Shift5, ShiftName = "Ca 5",
                    StartTime = new TimeOnly(17, 45), EndTime = new TimeOnly(19, 15), Description = "Tối sớm"   },
                new Shift { ShiftId = ShiftId.Shift6, ShiftName = "Ca 6",
                    StartTime = new TimeOnly(19, 30), EndTime = new TimeOnly(21,  0), Description = "Tối muộn"  }
            };
            context.Shifts.AddRange(shifts);
            context.SaveChanges();

            // =============================================
            // SEED ROOMS
            // =============================================
            var rooms = new List<Room>
            {
                new Room { RoomCode = "P101", RoomName = "Phòng 101", Capacity = 15, Note = "Phòng học chính - tầng 1" },
                new Room { RoomCode = "P102", RoomName = "Phòng 102", Capacity = 20, Note = "Phòng học lớn - tầng 1"  },
                new Room { RoomCode = "P201", RoomName = "Phòng 201", Capacity = 12, Note = "Phòng học nhỏ - tầng 2"  }
            };
            context.Rooms.AddRange(rooms);
            context.SaveChanges();

            // =============================================
            // SEED ACCOUNTS & USERS
            // =============================================

            // 1. ADMIN
            var adminAccount = new Account
            {
                Username = "admin",
                Password = BCrypt.Net.BCrypt.HashPassword("admin123"),
                Role = Role.Admin,
                IsActive = IsActive.Active,
                CreatedAt = DateTime.Now
            };
            context.Accounts.Add(adminAccount);
            context.SaveChanges();

            context.Staffs.Add(new Staff
            {
                AccountId = adminAccount.AccountId,
                Fullname = "Nguyễn Văn Admin",
                Dob = new DateTime(1990, 5, 15),
                Phone = "0901234567",
                Title = "Quản lý",
                Gender = Gender.Male
            });
            context.SaveChanges();

            // 2. TEACHERS
            var teacherAccounts = new List<Account>
            {
                new Account { Username = "teacher.math",    Password = BCrypt.Net.BCrypt.HashPassword("teacher123"), Role = Role.Teacher, IsActive = IsActive.Active },
                new Account { Username = "teacher.english", Password = BCrypt.Net.BCrypt.HashPassword("teacher123"), Role = Role.Teacher, IsActive = IsActive.Active }
            };
            context.Accounts.AddRange(teacherAccounts);
            context.SaveChanges();

            var teachers = new List<Teacher>
            {
                new Teacher
                {
                    AccountId = teacherAccounts[0].AccountId,
                    Fullname  = "Trần Thị Hoa",
                    Dob       = new DateTime(1988, 8, 20),
                    Phone     = "0912345678",
                    Gender    = Gender.Female,
                    Note      = "Giáo viên Toán 10 năm kinh nghiệm"
                },
                new Teacher
                {
                    AccountId = teacherAccounts[1].AccountId,
                    Fullname  = "Lê Văn Nam",
                    Dob       = new DateTime(1985, 3, 10),
                    Phone     = "0923456789",
                    Gender    = Gender.Male,
                    Note      = "Giáo viên Tiếng Anh chuyên IELTS"
                }
            };
            context.Teachers.AddRange(teachers);
            context.SaveChanges();

            // Teacher Subjects
            context.TeacherSubjects.AddRange(new List<TeacherSubject>
            {
                new TeacherSubject { TeacherId = teachers[0].AccountId, Subject = Subject.Math    },
                new TeacherSubject { TeacherId = teachers[0].AccountId, Subject = Subject.Physics },
                new TeacherSubject { TeacherId = teachers[1].AccountId, Subject = Subject.English    },
                new TeacherSubject { TeacherId = teachers[1].AccountId, Subject = Subject.Vietnamese }
            });
            context.SaveChanges();

            // 3. PARENT
            var parentAccount = new Account
            {
                Username = "parent.nguyen",
                Password = BCrypt.Net.BCrypt.HashPassword("parent123"),
                Role = Role.Parent,
                IsActive = IsActive.Active
            };
            context.Accounts.Add(parentAccount);
            context.SaveChanges();

            var parent = new Parent
            {
                AccountId = parentAccount.AccountId,
                Fullname = "Nguyễn Thị Mai",
                Dob = new DateTime(1980, 12, 5),
                Phone = "0934567890",
                Gender = Gender.Female,
                Note = "Phụ huynh học sinh lớp Toán 10"
            };
            context.Parents.Add(parent);
            context.SaveChanges();

            // 4. STUDENTS
            var studentAccounts = new List<Account>
            {
                new Account { Username = "student.an",   Password = BCrypt.Net.BCrypt.HashPassword("student123"), Role = Role.Student, IsActive = IsActive.Active },
                new Account { Username = "student.binh", Password = BCrypt.Net.BCrypt.HashPassword("student123"), Role = Role.Student, IsActive = IsActive.Active },
                new Account { Username = "student.chi",  Password = BCrypt.Net.BCrypt.HashPassword("student123"), Role = Role.Student, IsActive = IsActive.Active }
            };
            context.Accounts.AddRange(studentAccounts);
            context.SaveChanges();

            var students = new List<Student>
            {
                new Student
                {
                    AccountId     = studentAccounts[0].AccountId,
                    Fullname      = "Nguyễn Văn An",
                    Dob           = new DateTime(2010, 6, 15),
                    Phone         = "0945678901",
                    Gender        = Gender.Male,
                    CurrentSchool = "THPT Lê Hồng Phong",
                    ParentId      = parent.AccountId,
                    Note          = "Học sinh giỏi Toán"
                },
                new Student
                {
                    AccountId     = studentAccounts[1].AccountId,
                    Fullname      = "Trần Thị Bình",
                    Dob           = new DateTime(2011, 9, 20),
                    Phone         = "0956789012",
                    Gender        = Gender.Female,
                    CurrentSchool = "THPT Trần Phú",
                    ParentId      = parent.AccountId,
                    Note          = "Học sinh trung bình"
                },
                new Student
                {
                    AccountId     = studentAccounts[2].AccountId,
                    Fullname      = "Lê Văn Chi",
                    Dob           = new DateTime(2010, 3, 8),
                    Phone         = "0967890123",
                    Gender        = Gender.Male,
                    CurrentSchool = "THPT Nguyễn Huệ",
                    ParentId      = null,
                    Note          = "Học sinh khá"
                }
            };
            context.Students.AddRange(students);
            context.SaveChanges();

            // =============================================
            // SEED CLASSES
            // =============================================
            var classes = new List<Class>
            {
                new Class
                {
                    ClassCode   = "TOAN-10A",
                    Subject     = Subject.Math,
                    Status      = ClassStatus.Active,
                    GradeLevel  = 10,
                    ClassName   = "10A1",
                    Description = "Lớp Toán khối 10 - nâng cao"
                },
                new Class
                {
                    ClassCode   = "ANH-8B",
                    Subject     = Subject.English,
                    Status      = ClassStatus.Active,
                    GradeLevel  = 8,
                    ClassName   = "8B2",
                    Description = "Lớp Tiếng Anh khối 8 - luyện IELTS"
                }
            };
            context.Classes.AddRange(classes);
            context.SaveChanges();

            // ClassStudents
            context.ClassStudents.AddRange(new List<ClassStudent>
            {
                new ClassStudent { ClassId = classes[0].ClassId, StudentId = students[0].AccountId, StartedAt = new DateOnly(2025, 1, 15), Status = StudentClassStatus.Active },
                new ClassStudent { ClassId = classes[0].ClassId, StudentId = students[1].AccountId, StartedAt = new DateOnly(2025, 1, 15), Status = StudentClassStatus.Active },
                new ClassStudent { ClassId = classes[1].ClassId, StudentId = students[2].AccountId, StartedAt = new DateOnly(2025, 2,  1),  Status = StudentClassStatus.Active }
            });
            context.SaveChanges();

            // =============================================
            // SEED HOLIDAYS
            // =============================================
            context.Holidays.AddRange(new List<Holiday>
            {
                new Holiday
                {
                    HolidayName = "Tết Nguyên Đán 2025",
                    StartDate   = new DateOnly(2025, 1, 28),
                    EndDate     = new DateOnly(2025, 2, 5),
                    Description = "Nghỉ Tết Nguyên Đán - 9 ngày"
                },
                new Holiday
                {
                    HolidayName = "Giỗ Tổ Hùng Vương",
                    StartDate   = new DateOnly(2025, 4, 18),
                    EndDate     = new DateOnly(2025, 4, 18),
                    Description = "Nghỉ lễ Giỗ Tổ - 1 ngày"
                },
                new Holiday
                {
                    HolidayName = "Quốc Khánh",
                    StartDate   = new DateOnly(2025, 9, 2),
                    EndDate     = new DateOnly(2025, 9, 2),
                    Description = "Nghỉ lễ Quốc Khánh - 1 ngày"
                },
                new Holiday
                {
                    HolidayName = "Nghỉ hè",
                    StartDate   = new DateOnly(2025, 6, 1),
                    EndDate     = new DateOnly(2025, 8, 15),
                    Description = "Nghỉ hè - 2.5 tháng"
                }
            });
            context.SaveChanges();

            // =============================================
            // SEED WEEKLY SCHEDULE TEMPLATES
            // TeacherId gán trực tiếp vào template
            // teachers[0] = Trần Thị Hoa (Toán)  → TOAN-10A
            // teachers[1] = Lê Văn Nam (Anh)      → ANH-8B
            // =============================================
            var templates = new List<WeeklyScheduleTemplate>
            {
                // TOAN-10A: Thứ 2 - Ca 1 - P101
                new WeeklyScheduleTemplate
                {
                    ClassId   = classes[0].ClassId,
                    RoomId    = rooms[0].RoomId,
                    ShiftId   = ShiftId.Shift1,
                    StartDate = new DateOnly(2025, 2, 10),
                    EndDate   = new DateOnly(2025, 5, 30),
                    DayOfWeek = DayOfTheWeek.Monday,
                    Status    = ScheduleTemplateStatus.Active,
                    TeacherId = teachers[0].AccountId
                },
                // TOAN-10A: Thứ 4 - Ca 1 - P101
                new WeeklyScheduleTemplate
                {
                    ClassId   = classes[0].ClassId,
                    RoomId    = rooms[0].RoomId,
                    ShiftId   = ShiftId.Shift1,
                    StartDate = new DateOnly(2025, 2, 10),
                    EndDate   = new DateOnly(2025, 5, 30),
                    DayOfWeek = DayOfTheWeek.Wednesday,
                    Status    = ScheduleTemplateStatus.Active,
                    TeacherId = teachers[0].AccountId
                },
                // TOAN-10A: Thứ 6 - Ca 1 - P101
                new WeeklyScheduleTemplate
                {
                    ClassId   = classes[0].ClassId,
                    RoomId    = rooms[0].RoomId,
                    ShiftId   = ShiftId.Shift1,
                    StartDate = new DateOnly(2025, 2, 10),
                    EndDate   = new DateOnly(2025, 5, 30),
                    DayOfWeek = DayOfTheWeek.Friday,
                    Status    = ScheduleTemplateStatus.Active,
                    TeacherId = teachers[0].AccountId
                },
                // ANH-8B: Thứ 3 - Ca 5 - P102
                new WeeklyScheduleTemplate
                {
                    ClassId   = classes[1].ClassId,
                    RoomId    = rooms[1].RoomId,
                    ShiftId   = ShiftId.Shift5,
                    StartDate = new DateOnly(2025, 2, 10),
                    EndDate   = new DateOnly(2025, 6, 30),
                    DayOfWeek = DayOfTheWeek.Tuesday,
                    Status    = ScheduleTemplateStatus.Active,
                    TeacherId = teachers[1].AccountId
                },
                // ANH-8B: Thứ 5 - Ca 5 - P102
                new WeeklyScheduleTemplate
                {
                    ClassId   = classes[1].ClassId,
                    RoomId    = rooms[1].RoomId,
                    ShiftId   = ShiftId.Shift5,
                    StartDate = new DateOnly(2025, 2, 10),
                    EndDate   = new DateOnly(2025, 6, 30),
                    DayOfWeek = DayOfTheWeek.Thursday,
                    Status    = ScheduleTemplateStatus.Active,
                    TeacherId = teachers[1].AccountId
                }
            };
            context.WeeklyScheduleTemplates.AddRange(templates);
            context.SaveChanges();

            // =============================================
            // SEED SAMPLE SESSIONS
            // TeacherId gán trực tiếp vào session
            // =============================================
            var sessions = new List<Session>
            {
                // TOAN-10A - Buổi 1 (Thứ 2, 10/2) - GV: Trần Thị Hoa
                new Session
                {
                    ClassId     = classes[0].ClassId,
                    RoomId      = rooms[0].RoomId,
                    ShiftId     = ShiftId.Shift1,
                    TemplateId  = templates[0].TemplateId,
                    TeacherId   = teachers[0].AccountId,
                    SessionDate = new DateOnly(2025, 2, 10),
                    Status      = SessionStatus.Completed,
                    CreatedAt   = DateTime.Now
                },
                // TOAN-10A - Buổi 2 (Thứ 4, 12/2) - GV: Trần Thị Hoa
                new Session
                {
                    ClassId     = classes[0].ClassId,
                    RoomId      = rooms[0].RoomId,
                    ShiftId     = ShiftId.Shift1,
                    TemplateId  = templates[1].TemplateId,
                    TeacherId   = teachers[0].AccountId,
                    SessionDate = new DateOnly(2025, 2, 12),
                    Status      = SessionStatus.Completed,
                    CreatedAt   = DateTime.Now
                },
                // ANH-8B - Buổi 1 (Thứ 3, 11/2) - GV: Lê Văn Nam
                new Session
                {
                    ClassId     = classes[1].ClassId,
                    RoomId      = rooms[1].RoomId,
                    ShiftId     = ShiftId.Shift5,
                    TemplateId  = templates[3].TemplateId,
                    TeacherId   = teachers[1].AccountId,
                    SessionDate = new DateOnly(2025, 2, 11),
                    Status      = SessionStatus.Scheduled,
                    CreatedAt   = DateTime.Now
                }
            };
            context.Sessions.AddRange(sessions);
            context.SaveChanges();

            // =============================================
            // SEED ATTENDANCES (cho 2 buổi Completed)
            // =============================================
            context.Attendances.AddRange(new List<Attendance>
            {
                // Buổi 1 TOAN-10A
                new Attendance { SessionId = sessions[0].SessionId, StudentId = students[0].AccountId, Status = AttendanceStatus.Present, CreatedBy = adminAccount.AccountId },
                new Attendance { SessionId = sessions[0].SessionId, StudentId = students[1].AccountId, Status = AttendanceStatus.Present, CreatedBy = adminAccount.AccountId },
                // Buổi 2 TOAN-10A
                new Attendance { SessionId = sessions[1].SessionId, StudentId = students[0].AccountId, Status = AttendanceStatus.Present, CreatedBy = adminAccount.AccountId },
                new Attendance { SessionId = sessions[1].SessionId, StudentId = students[1].AccountId, Status = AttendanceStatus.Absent,  CreatedBy = adminAccount.AccountId }
            });
            context.SaveChanges();

            Console.WriteLine("✅ Database seeded successfully!");
        }
    }
}