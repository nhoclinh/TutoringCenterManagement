using TutoringCenterManagement.Services.Implementations;

namespace TutoringCenterManagement.Services.Interfaces
{
    public interface IUserImportExportService
    {
        /// <summary>
        /// Xuất toàn bộ người dùng (Staff, Teacher, Student, Parent) ra file Excel.
        /// </summary>
        Task<byte[]> ExportAllUsersAsync();

        /// <summary>
        /// Nhập người dùng từ file Excel — trả về kết quả success/failed/errors.
        /// </summary>
        Task<UserImportExportService.ImportResult> ImportUsersAsync(Stream fileStream);

        /// <summary>
        /// Tạo file Excel mẫu có dropdown validation để người dùng điền vào.
        /// </summary>
        byte[] GenerateImportTemplate();
    }
}