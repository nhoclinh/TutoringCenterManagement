using Microsoft.EntityFrameworkCore;
using TutoringCenterManagement.Data.Entities;
using TutoringCenterManagement.Data.Enums;

namespace TutoringCenterManagement.Data
{
    /// <summary>
    /// Database Context - Quản lý kết nối và mapping với SQL Server
    /// </summary>
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // DbSets - Các bảng trong database
        public DbSet<Account> Accounts { get; set; }
        public DbSet<Staff> Staffs { get; set; }
        public DbSet<Teacher> Teachers { get; set; }
        public DbSet<Student> Students { get; set; }
        public DbSet<Parent> Parents { get; set; }
        public DbSet<Shift> Shifts { get; set; }
        public DbSet<Room> Rooms { get; set; }
        public DbSet<Holiday> Holidays { get; set; }
        public DbSet<Class> Classes { get; set; }
        public DbSet<WeeklyScheduleTemplate> WeeklyScheduleTemplates { get; set; }
        public DbSet<Session> Sessions { get; set; }
        public DbSet<ClassStudent> ClassStudents { get; set; }
        public DbSet<Attendance> Attendances { get; set; }
        public DbSet<TeacherSubject> TeacherSubjects { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // =============================================
            // CẤU HÌNH RELATIONSHIPS VÀ CONSTRAINTS
            // =============================================

            #region Account Relationships (1-1 với Staff/Teacher/Student/Parent)

            // Account - Staff (1-1)
            modelBuilder.Entity<Account>()
                .HasOne(a => a.Staff)
                .WithOne(s => s.Account)
                .HasForeignKey<Staff>(s => s.AccountId)
                .OnDelete(DeleteBehavior.Cascade);

            // Account - Teacher (1-1)
            modelBuilder.Entity<Account>()
                .HasOne(a => a.Teacher)
                .WithOne(t => t.Account)
                .HasForeignKey<Teacher>(t => t.AccountId)
                .OnDelete(DeleteBehavior.Cascade);

            // Account - Student (1-1)
            modelBuilder.Entity<Account>()
                .HasOne(a => a.Student)
                .WithOne(s => s.Account)
                .HasForeignKey<Student>(s => s.AccountId)
                .OnDelete(DeleteBehavior.Cascade);

            // Account - Parent (1-1)
            modelBuilder.Entity<Account>()
                .HasOne(a => a.Parent)
                .WithOne(p => p.Account)
                .HasForeignKey<Parent>(p => p.AccountId)
                .OnDelete(DeleteBehavior.Cascade);

            #endregion

            #region Student - Parent Relationship (Many-to-1)

            modelBuilder.Entity<Student>()
                .HasOne(s => s.Parent)
                .WithMany(p => p.Students)
                .HasForeignKey(s => s.ParentId)
                .OnDelete(DeleteBehavior.NoAction);

            #endregion

            #region ClassStudent - Composite Key & Relationships

            // Composite Primary Key
            modelBuilder.Entity<ClassStudent>()
                .HasKey(cs => new { cs.ClassId, cs.StudentId });

            // ClassStudent - Class
            modelBuilder.Entity<ClassStudent>()
                .HasOne(cs => cs.Class)
                .WithMany(c => c.ClassStudents)
                .HasForeignKey(cs => cs.ClassId)
                .OnDelete(DeleteBehavior.Cascade);

            // ClassStudent - Student
            modelBuilder.Entity<ClassStudent>()
                .HasOne(cs => cs.Student)
                .WithMany(s => s.ClassStudents)
                .HasForeignKey(cs => cs.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            #endregion

            #region Class Relationships

            // Class - WeeklyScheduleTemplate (1-Many)
            modelBuilder.Entity<WeeklyScheduleTemplate>()
                .HasOne(wst => wst.Class)
                .WithMany(c => c.WeeklyScheduleTemplates)
                .HasForeignKey(wst => wst.ClassId)
                .OnDelete(DeleteBehavior.Cascade);

            // Class - Session (1-Many)
            modelBuilder.Entity<Session>()
                .HasOne(s => s.Class)
                .WithMany(c => c.Sessions)
                .HasForeignKey(s => s.ClassId)
                .OnDelete(DeleteBehavior.Restrict);

            #endregion

            #region Shift Relationships

            // Shift - WeeklyScheduleTemplate (1-Many)
            modelBuilder.Entity<WeeklyScheduleTemplate>()
                .HasOne(wst => wst.Shift)
                .WithMany(sh => sh.WeeklyScheduleTemplates)
                .HasForeignKey(wst => wst.ShiftId)
                .OnDelete(DeleteBehavior.Restrict);

            // Shift - Session (1-Many)
            modelBuilder.Entity<Session>()
                .HasOne(s => s.Shift)
                .WithMany(sh => sh.Sessions)
                .HasForeignKey(s => s.ShiftId)
                .OnDelete(DeleteBehavior.Restrict);

            #endregion

            #region Room Relationships

            // Room - WeeklyScheduleTemplate (1-Many)
            modelBuilder.Entity<WeeklyScheduleTemplate>()
                .HasOne(wst => wst.Room)
                .WithMany()
                .HasForeignKey(wst => wst.RoomId)
                .OnDelete(DeleteBehavior.Restrict);

            // Room - Session (1-Many)
            modelBuilder.Entity<Session>()
                .HasOne(s => s.Room)
                .WithMany(r => r.Sessions)
                .HasForeignKey(s => s.RoomId)
                .OnDelete(DeleteBehavior.Restrict);

            #endregion

            #region Session Relationships

            // Session - WeeklyScheduleTemplate (Many-1, nullable)
            modelBuilder.Entity<Session>()
                .HasOne(s => s.Template)
                .WithMany(wst => wst.Sessions)
                .HasForeignKey(s => s.TemplateId)
                .OnDelete(DeleteBehavior.SetNull);

            // Session - Teacher (giáo viên chính, bắt buộc)
            modelBuilder.Entity<Session>()
                .HasOne(s => s.Teacher)
                .WithMany(t => t.Sessions)
                .HasForeignKey(s => s.TeacherId)
                .OnDelete(DeleteBehavior.Restrict);

            // Session - TeacherAssistant (trợ giảng, nullable)
            modelBuilder.Entity<Session>()
                .HasOne(s => s.TeacherAssistant)
                .WithMany(t => t.AssistantSessions)
                .HasForeignKey(s => s.TeacherAssistantId)
                .OnDelete(DeleteBehavior.Restrict);

            #endregion

            #region WeeklyScheduleTemplate - Teacher Relationships

            // Template - Teacher (giáo viên chính, bắt buộc)
            modelBuilder.Entity<WeeklyScheduleTemplate>()
                .HasOne(wst => wst.Teacher)
                .WithMany(t => t.Templates)
                .HasForeignKey(wst => wst.TeacherId)
                .OnDelete(DeleteBehavior.Restrict);

            // Template - TeacherAssistant (trợ giảng, nullable)
            modelBuilder.Entity<WeeklyScheduleTemplate>()
                .HasOne(wst => wst.TeacherAssistant)
                .WithMany(t => t.AssistantTemplates)
                .HasForeignKey(wst => wst.TeacherAssistantId)
                .OnDelete(DeleteBehavior.Restrict);

            #endregion

            #region Attendance Relationships

            // Attendance - Session (Many-1)
            modelBuilder.Entity<Attendance>()
                .HasOne(a => a.Session)
                .WithMany(s => s.Attendances)
                .HasForeignKey(a => a.SessionId)
                .OnDelete(DeleteBehavior.Cascade);

            // Attendance - Student (Many-1)
            modelBuilder.Entity<Attendance>()
                .HasOne(a => a.Student)
                .WithMany(s => s.Attendances)
                .HasForeignKey(a => a.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            // Attendance - Account (CreatedBy)
            modelBuilder.Entity<Attendance>()
                .HasOne(a => a.CreatedByAccount)
                .WithMany(acc => acc.CreatedAttendances)
                .HasForeignKey(a => a.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict);

            // Attendance - Account (LastUpdatedBy)
            modelBuilder.Entity<Attendance>()
                .HasOne(a => a.LastUpdatedByAccount)
                .WithMany(acc => acc.LastUpdatedAttendances)
                .HasForeignKey(a => a.LastUpdatedBy)
                .OnDelete(DeleteBehavior.Restrict);

            #endregion

            #region TeacherSubject - Composite Key & Relationships

            modelBuilder.Entity<TeacherSubject>()
                .HasKey(ts => new { ts.TeacherId, ts.Subject });

            modelBuilder.Entity<TeacherSubject>()
                .HasOne(ts => ts.Teacher)
                .WithMany(t => t.TeacherSubjects)
                .HasForeignKey(ts => ts.TeacherId)
                .OnDelete(DeleteBehavior.Cascade);

            #endregion

            #region Indexes (Tối ưu query)

            // Unique Username
            modelBuilder.Entity<Account>()
                .HasIndex(a => a.Username)
                .IsUnique();

            // Unique RoomCode
            modelBuilder.Entity<Room>()
                .HasIndex(r => r.RoomCode)
                .IsUnique();

            // Unique ClassCode
            modelBuilder.Entity<Class>()
                .HasIndex(c => c.ClassCode)
                .IsUnique();

            // Index cho query nhanh
            modelBuilder.Entity<Session>()
                .HasIndex(s => s.SessionDate);

            modelBuilder.Entity<Session>()
                .HasIndex(s => s.TeacherId);

            modelBuilder.Entity<Holiday>()
                .HasIndex(h => new { h.StartDate, h.EndDate });

            modelBuilder.Entity<Attendance>()
                .HasIndex(a => new { a.SessionId, a.StudentId })
                .IsUnique(); // Mỗi học sinh chỉ điểm danh 1 lần/buổi

            #endregion

            #region Default Values & Check Constraints

            // Room Capacity > 0
            modelBuilder.Entity<Room>()
                .ToTable(t => t.HasCheckConstraint("CK_Room_Capacity", "[Capacity] > 0"));

            // Holiday: EndDate >= StartDate
            modelBuilder.Entity<Holiday>()
                .ToTable(t => t.HasCheckConstraint("CK_Holiday_DateRange", "[EndDate] >= [StartDate]"));

            #endregion
        }
    }
}