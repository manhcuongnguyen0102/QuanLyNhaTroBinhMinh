using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace QuanLyNhaTroBinhMinh.Models;

public partial class QuanLyNhaTroBinhMinhContext : DbContext
{
    public QuanLyNhaTroBinhMinhContext()
    {
    }

    public QuanLyNhaTroBinhMinhContext(DbContextOptions<QuanLyNhaTroBinhMinhContext> options)
        : base(options)
    {
    }

    public virtual DbSet<ChiSoDienNuoc> ChiSoDienNuocs { get; set; }

    public virtual DbSet<ChiTietHoaDon> ChiTietHoaDons { get; set; }

    public virtual DbSet<DichVu> DichVus { get; set; }

    public virtual DbSet<DichVuHopDong> DichVuHopDongs { get; set; }

    public virtual DbSet<HoaDon> HoaDons { get; set; }

    public virtual DbSet<HopDong> HopDongs { get; set; }

    public virtual DbSet<KhachO> KhachOs { get; set; }

    public virtual DbSet<NguoiDung> NguoiDungs { get; set; }

    public virtual DbSet<PhongTro> PhongTros { get; set; }

    public virtual DbSet<SuCo> SuCos { get; set; }

    public virtual DbSet<ThanhToan> ThanhToans { get; set; }

    public virtual DbSet<ThongBao> ThongBaos { get; set; }

    public virtual DbSet<VaiTro> VaiTros { get; set; }

    /*protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Server=localhost\\SQLEXPRESS;Database=QuanLyNhaTroBinhMinh;Trusted_Connection=True;TrustServerCertificate=True;");
    */
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ChiSoDienNuoc>(entity =>
        {
            entity.HasKey(e => e.MaChiSo).HasName("PK__ChiSoDie__EBA18E15F5198AD3");

            entity.ToTable("ChiSoDienNuoc");

            entity.Property(e => e.MaChiSo).HasMaxLength(20);
            entity.Property(e => e.AnhDongHo).HasMaxLength(255);
            entity.Property(e => e.MaPhong).HasMaxLength(20);

            entity.HasOne(d => d.MaPhongNavigation).WithMany(p => p.ChiSoDienNuocs)
                .HasForeignKey(d => d.MaPhong)
                .HasConstraintName("FK__ChiSoDien__MaPho__6D0D32F4");
        });

        modelBuilder.Entity<ChiTietHoaDon>(entity =>
        {
            entity.HasKey(e => e.MaChiTiet).HasName("PK__ChiTietH__CDF0A11411F6CDDA");

            entity.ToTable("ChiTietHoaDon");

            entity.Property(e => e.MaChiTiet).HasMaxLength(20);
            entity.Property(e => e.DaXoa).HasDefaultValue(false);
            entity.Property(e => e.DonGia).HasColumnType("decimal(18, 0)");
            entity.Property(e => e.MaDichVu).HasMaxLength(20);
            entity.Property(e => e.MaHoaDon).HasMaxLength(20);
            entity.Property(e => e.ThanhTien).HasColumnType("decimal(18, 0)");

            entity.HasOne(d => d.MaDichVuNavigation).WithMany(p => p.ChiTietHoaDons)
                .HasForeignKey(d => d.MaDichVu)
                .HasConstraintName("FK__ChiTietHo__MaDic__7E37BEF6");

            entity.HasOne(d => d.MaHoaDonNavigation).WithMany(p => p.ChiTietHoaDons)
                .HasForeignKey(d => d.MaHoaDon)
                .HasConstraintName("FK__ChiTietHo__MaHoa__7D439ABD");
        });

        modelBuilder.Entity<DichVu>(entity =>
        {
            entity.HasKey(e => e.MaDichVu).HasName("PK__DichVu__C0E6DE8F13F919C0");

            entity.ToTable("DichVu");

            entity.Property(e => e.MaDichVu).HasMaxLength(20);
            entity.Property(e => e.DaXoa).HasDefaultValue(false);
            entity.Property(e => e.DonGia).HasColumnType("decimal(18, 0)");
            entity.Property(e => e.DonViTinh).HasMaxLength(20);
            entity.Property(e => e.LaCoDinh).HasDefaultValue(true);
            entity.Property(e => e.TenDichVu).HasMaxLength(50);
        });

        modelBuilder.Entity<DichVuHopDong>(entity =>
        {
            entity.HasKey(e => e.MaDvHd).HasName("PK__DichVu_H__6ED6334F63060271");

            entity.ToTable("DichVu_HopDong");

            entity.Property(e => e.MaDvHd)
                .HasMaxLength(20)
                .HasColumnName("MaDV_HD");
            entity.Property(e => e.MaDichVu).HasMaxLength(20);
            entity.Property(e => e.MaHopDong).HasMaxLength(20);

            entity.HasOne(d => d.MaDichVuNavigation).WithMany(p => p.DichVuHopDongs)
                .HasForeignKey(d => d.MaDichVu)
                .HasConstraintName("FK__DichVu_Ho__MaDic__6A30C649");

            entity.HasOne(d => d.MaHopDongNavigation).WithMany(p => p.DichVuHopDongs)
                .HasForeignKey(d => d.MaHopDong)
                .HasConstraintName("FK__DichVu_Ho__MaHop__693CA210");
        });

        modelBuilder.Entity<HoaDon>(entity =>
        {
            entity.HasKey(e => e.MaHoaDon).HasName("PK__HoaDon__835ED13B7C835DB5");

            entity.ToTable("HoaDon");

            entity.Property(e => e.MaHoaDon).HasMaxLength(20);
            entity.Property(e => e.DaXoa).HasDefaultValue(false);
            entity.Property(e => e.MaHopDong).HasMaxLength(20);
            entity.Property(e => e.NgayTao)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.TongTien)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 0)");
            entity.Property(e => e.TrangThai)
                .HasMaxLength(30)
                .HasDefaultValue("Chưa thanh toán");

            entity.HasOne(d => d.MaHopDongNavigation).WithMany(p => p.HoaDons)
                .HasForeignKey(d => d.MaHopDong)
                .HasConstraintName("FK__HoaDon__MaHopDon__74AE54BC");
        });

        modelBuilder.Entity<HopDong>(entity =>
        {
            entity.HasKey(e => e.MaHopDong).HasName("PK__HopDong__36DD43421D95B8E6");

            entity.ToTable("HopDong");

            entity.Property(e => e.MaHopDong).HasMaxLength(20);
            entity.Property(e => e.MaNguoiDungDaiDien)
                .HasMaxLength(20)
                .HasColumnName("MaNguoiDung_DaiDien");
            entity.Property(e => e.MaPhong).HasMaxLength(20);
            entity.Property(e => e.TienCoc)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 0)");
            entity.Property(e => e.TrangThai)
                .HasMaxLength(20)
                .HasDefaultValue("Hiệu lực");

            entity.HasOne(d => d.MaNguoiDungDaiDienNavigation).WithMany(p => p.HopDongs)
                .HasForeignKey(d => d.MaNguoiDungDaiDien)
                .HasConstraintName("FK__HopDong__MaNguoi__59063A47");

            entity.HasOne(d => d.MaPhongNavigation).WithMany(p => p.HopDongs)
                .HasForeignKey(d => d.MaPhong)
                .HasConstraintName("FK__HopDong__MaPhong__5812160E");
        });

        modelBuilder.Entity<KhachO>(entity =>
        {
            entity.HasKey(e => e.MaKhach).HasName("PK__KhachO__D0CB8DDD45F2FB96");

            entity.ToTable("KhachO");

            entity.Property(e => e.MaKhach).HasMaxLength(20);
            entity.Property(e => e.Cccd)
                .HasMaxLength(12)
                .HasColumnName("CCCD");
            entity.Property(e => e.DaDangKyTamTru).HasDefaultValue(false);
            entity.Property(e => e.DaXoa).HasDefaultValue(false);
            entity.Property(e => e.HoTen).HasMaxLength(100);
            entity.Property(e => e.MaHopDong).HasMaxLength(20);
            entity.Property(e => e.SoDienThoai).HasMaxLength(15);

            entity.HasOne(d => d.MaHopDongNavigation).WithMany(p => p.KhachOs)
                .HasForeignKey(d => d.MaHopDong)
                .HasConstraintName("FK__KhachO__MaHopDon__60A75C0F");
        });

        modelBuilder.Entity<NguoiDung>(entity =>
        {
            entity.HasKey(e => e.MaNguoiDung).HasName("PK__NguoiDun__C539D762E38C77AD");

            entity.ToTable("NguoiDung");

            entity.HasIndex(e => e.TenDangNhap, "UQ__NguoiDun__55F68FC08E5B7C63").IsUnique();

            entity.Property(e => e.MaNguoiDung).HasMaxLength(20);
            entity.Property(e => e.MaVaiTro).HasMaxLength(20);
            entity.Property(e => e.MatKhau).HasMaxLength(255);
            entity.Property(e => e.TenDangNhap).HasMaxLength(50);
            entity.Property(e => e.TrangThai)
                .HasMaxLength(20)
                .HasDefaultValue("Hoạt động");

            entity.HasOne(d => d.MaVaiTroNavigation).WithMany(p => p.NguoiDungs)
                .HasForeignKey(d => d.MaVaiTro)
                .HasConstraintName("FK__NguoiDung__MaVai__4D94879B");
        });

        modelBuilder.Entity<PhongTro>(entity =>
        {
            entity.HasKey(e => e.MaPhong).HasName("PK__PhongTro__20BD5E5BE35325EE");

            entity.ToTable("PhongTro");

            entity.Property(e => e.MaPhong).HasMaxLength(20);
            entity.Property(e => e.GiaThueCung).HasColumnType("decimal(18, 0)");
            entity.Property(e => e.TenPhong).HasMaxLength(50);
            entity.Property(e => e.TrangThai)
                .HasMaxLength(20)
                .HasDefaultValue("Trống");
        });

        modelBuilder.Entity<SuCo>(entity =>
        {
            entity.HasKey(e => e.MaSuCo).HasName("PK__SuCo__A69DF79F3D1B7153");

            entity.ToTable("SuCo");

            entity.Property(e => e.MaSuCo).HasMaxLength(20);
            entity.Property(e => e.ChiPhiSua)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 0)");
            entity.Property(e => e.DaXoa).HasDefaultValue(false);
            entity.Property(e => e.MaNguoiDungBao)
                .HasMaxLength(20)
                .HasColumnName("MaNguoiDung_Bao");
            entity.Property(e => e.MaPhong).HasMaxLength(20);
            entity.Property(e => e.MoTa).HasMaxLength(500);
            entity.Property(e => e.NgayTao)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.TrangThai)
                .HasMaxLength(30)
                .HasDefaultValue("Chờ xử lý");

            entity.HasOne(d => d.MaNguoiDungBaoNavigation).WithMany(p => p.SuCos)
                .HasForeignKey(d => d.MaNguoiDungBao)
                .HasConstraintName("FK__SuCo__MaNguoiDun__08B54D69");

            entity.HasOne(d => d.MaPhongNavigation).WithMany(p => p.SuCos)
                .HasForeignKey(d => d.MaPhong)
                .HasConstraintName("FK__SuCo__MaPhong__07C12930");
        });

        modelBuilder.Entity<ThanhToan>(entity =>
        {
            entity.HasKey(e => e.MaThanhToan).HasName("PK__ThanhToa__D4B25844B0288E27");

            entity.ToTable("ThanhToan");

            entity.Property(e => e.MaThanhToan).HasMaxLength(20);
            entity.Property(e => e.DaXoa).HasDefaultValue(false);
            entity.Property(e => e.MaHoaDon).HasMaxLength(20);
            entity.Property(e => e.NgayTra)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.PhuongThuc).HasMaxLength(50);
            entity.Property(e => e.SoTienTra).HasColumnType("decimal(18, 0)");

            entity.HasOne(d => d.MaHoaDonNavigation).WithMany(p => p.ThanhToans)
                .HasForeignKey(d => d.MaHoaDon)
                .HasConstraintName("FK__ThanhToan__MaHoa__02084FDA");
        });

        modelBuilder.Entity<ThongBao>(entity =>
        {
            entity.HasKey(e => e.MaThongBao).HasName("PK__ThongBao__04DEB54E7E280AE0");

            entity.ToTable("ThongBao");

            entity.Property(e => e.MaThongBao).HasMaxLength(20);
            entity.Property(e => e.DaXoa).HasDefaultValue(false);
            entity.Property(e => e.MaNguoiDungTao)
                .HasMaxLength(20)
                .HasColumnName("MaNguoiDung_Tao");
            entity.Property(e => e.NgayTao)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.TieuDe).HasMaxLength(200);

            entity.HasOne(d => d.MaNguoiDungTaoNavigation).WithMany(p => p.ThongBaos)
                .HasForeignKey(d => d.MaNguoiDungTao)
                .HasConstraintName("FK__ThongBao__MaNguo__0F624AF8");
        });

        modelBuilder.Entity<VaiTro>(entity =>
        {
            entity.HasKey(e => e.MaVaiTro).HasName("PK__VaiTro__C24C41CFF0F94D05");

            entity.ToTable("VaiTro");

            entity.Property(e => e.MaVaiTro).HasMaxLength(20);
            entity.Property(e => e.TenVaiTro).HasMaxLength(50);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
