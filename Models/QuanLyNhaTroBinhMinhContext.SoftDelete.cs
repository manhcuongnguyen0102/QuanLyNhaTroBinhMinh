using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace QuanLyNhaTroBinhMinh.Models;

/// <summary>
/// Mở rộng DbContext bằng partial class để cấu hình Xóa mềm (Soft Delete) tập trung.
/// Đảm bảo an toàn tuyệt đối khi chạy lại lệnh Scaffold-DbContext.
/// </summary>
public partial class QuanLyNhaTroBinhMinhContext
{
    private const string SoftDeleteColumnName = "DaXoa";

 
    // Tự động gắn Global Query Filter (WHERE DaXoa != 1) kiểm tra xem trong DATABASE có những
    // bảng nào có cột DAXOA
  
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())//trả về 1 ds IENum chứa các bảng
        {
            // Kiểm tra xem bảng hiện tại có cột "DaXoa" (kiểu bool hoặc bool?) hay không
            var daXoaProperty = entityType.FindProperty(SoftDeleteColumnName);// gan cac bang có cột đã xóa

            if (daXoaProperty != null &&
               (daXoaProperty.ClrType == typeof(bool?) || daXoaProperty.ClrType == typeof(bool)))// trả về kiểu dữ liệu của cột đã xóa
            {
                // Gọi hàm generic để gắn bộ lọc tự động cho bảng đó
                var method = typeof(QuanLyNhaTroBinhMinhContext)
                    .GetMethod(nameof(ApplySoftDeleteFilter), BindingFlags.NonPublic | BindingFlags.Static)?
                    .MakeGenericMethod(entityType.ClrType);

                method?.Invoke(null, new object[] { modelBuilder });
            }
        }
    }

    private static void ApplySoftDeleteFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class
    {
        // Lấy tất cả bản ghi có DaXoa là false hoặc NULL (chưa bị xóa)
        modelBuilder.Entity<TEntity>()
            .HasQueryFilter(e => EF.Property<bool?>(e, SoftDeleteColumnName) != true);
    }

    //  Tự động chuyển lệnh Delete thành Update DaXoa = true
    
    public override int SaveChanges()
    {
        ConvertHardDeleteToSoftDelete();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ConvertHardDeleteToSoftDelete();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void ConvertHardDeleteToSoftDelete()
    {
        // Quét tất cả các thực thể đang bị gọi lệnh _context.Remove()
        var deletedEntries = ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Deleted);

        foreach (EntityEntry entry in deletedEntries)
        {
            var daXoaProp = entry.Metadata.FindProperty(SoftDeleteColumnName);

            // Nếu bảng có cột DaXoa -> Chặn xóa cứng, chuyển thành cập nhật DaXoa = true
            if (daXoaProp != null)
            {
                entry.State = EntityState.Modified;
                entry.CurrentValues[SoftDeleteColumnName] = true;
            }
        }
    }
}