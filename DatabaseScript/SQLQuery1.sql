CREATE DATABASE QuanLyNhaTroBinhMinh;
GO
USE QuanLyNhaTroBinhMinh;
GO

-- ==============================================
-- 1. TẠO CÁC BẢNG & RÀNG BUỘC
-- ==============================================
CREATE TABLE VaiTro (
    MaVaiTro NVARCHAR(20) PRIMARY KEY,
    TenVaiTro NVARCHAR(50) NOT NULL
);

CREATE TABLE NguoiDung (
    MaNguoiDung NVARCHAR(20) PRIMARY KEY,
    TenDangNhap NVARCHAR(50) UNIQUE NOT NULL,
    MatKhau NVARCHAR(255) NOT NULL,
    MaVaiTro NVARCHAR(20) FOREIGN KEY REFERENCES VaiTro(MaVaiTro),
    TrangThai NVARCHAR(20) DEFAULT N'Hoạt động' CHECK (TrangThai IN (N'Hoạt động', N'Khóa'))
);

CREATE TABLE PhongTro (
    MaPhong NVARCHAR(20) PRIMARY KEY,
    TenPhong NVARCHAR(50) NOT NULL,
    DienTich FLOAT CHECK (DienTich > 0),
    GiaThueCung DECIMAL(18, 0) NOT NULL CHECK (GiaThueCung >= 0),
    TrangThai NVARCHAR(20) DEFAULT N'Trống' CHECK (TrangThai IN (N'Trống', N'Đang thuê', N'Đang sửa'))
);

CREATE TABLE HopDong (
    MaHopDong NVARCHAR(20) PRIMARY KEY,
    MaPhong NVARCHAR(20) FOREIGN KEY REFERENCES PhongTro(MaPhong),
    MaNguoiDung_DaiDien NVARCHAR(20) FOREIGN KEY REFERENCES NguoiDung(MaNguoiDung),
    NgayBatDau DATE NOT NULL,
    NgayKetThuc DATE NOT NULL,
    TienCoc DECIMAL(18, 0) DEFAULT 0 CHECK (TienCoc >= 0),
    TrangThai NVARCHAR(20) DEFAULT N'Hiệu lực' CHECK (TrangThai IN (N'Hiệu lực', N'Đã thanh lý')),
    CONSTRAINT CHK_NgayHopDong CHECK (NgayKetThuc > NgayBatDau)
);

CREATE TABLE KhachO (
    MaKhach NVARCHAR(20) PRIMARY KEY,
    MaHopDong NVARCHAR(20) FOREIGN KEY REFERENCES HopDong(MaHopDong),
    HoTen NVARCHAR(100) NOT NULL,
    CCCD NVARCHAR(12) NOT NULL CHECK (LEN(CCCD) = 12),
    SoDienThoai NVARCHAR(15),
    DaDangKyTamTru BIT DEFAULT 0 
);

CREATE TABLE DichVu (
    MaDichVu NVARCHAR(20) PRIMARY KEY,
    TenDichVu NVARCHAR(50) NOT NULL,
    DonViTinh NVARCHAR(20) NOT NULL,
    DonGia DECIMAL(18, 0) NOT NULL CHECK (DonGia >= 0),
    LaCoDinh BIT DEFAULT 1 
);

CREATE TABLE DichVu_HopDong (
    MaDV_HD NVARCHAR(20) PRIMARY KEY,
    MaHopDong NVARCHAR(20) FOREIGN KEY REFERENCES HopDong(MaHopDong),
    MaDichVu NVARCHAR(20) FOREIGN KEY REFERENCES DichVu(MaDichVu)
);

CREATE TABLE ChiSoDienNuoc (
    MaChiSo NVARCHAR(20) PRIMARY KEY,
    MaPhong NVARCHAR(20) FOREIGN KEY REFERENCES PhongTro(MaPhong),
    Thang INT CHECK (Thang BETWEEN 1 AND 12),
    Nam INT CHECK (Nam > 2000),
    DienCu INT CHECK (DienCu >= 0),
    DienMoi INT,
    NuocCu INT CHECK (NuocCu >= 0),
    NuocMoi INT,
    AnhDongHo NVARCHAR(255),
    CONSTRAINT CHK_ChiSo CHECK (DienMoi >= DienCu AND NuocMoi >= NuocCu)
);

CREATE TABLE HoaDon (
    MaHoaDon NVARCHAR(20) PRIMARY KEY,
    MaHopDong NVARCHAR(20) FOREIGN KEY REFERENCES HopDong(MaHopDong),
    Thang INT CHECK (Thang BETWEEN 1 AND 12),
    Nam INT CHECK (Nam > 2000),
    TongTien DECIMAL(18, 0) DEFAULT 0,
    TrangThai NVARCHAR(30) DEFAULT N'Chưa thanh toán' CHECK (TrangThai IN (N'Chưa thanh toán', N'Đã thanh toán', N'Thanh toán một phần')),
    NgayTao DATETIME DEFAULT GETDATE()
);

CREATE TABLE ChiTietHoaDon (
    MaChiTiet NVARCHAR(20) PRIMARY KEY,
    MaHoaDon NVARCHAR(20) FOREIGN KEY REFERENCES HoaDon(MaHoaDon),
    MaDichVu NVARCHAR(20) FOREIGN KEY REFERENCES DichVu(MaDichVu),
    SoLuong FLOAT NOT NULL CHECK (SoLuong > 0),
    DonGia DECIMAL(18, 0) NOT NULL,
    ThanhTien DECIMAL(18, 0) NOT NULL
);

CREATE TABLE ThanhToan (
    MaThanhToan NVARCHAR(20) PRIMARY KEY,
    MaHoaDon NVARCHAR(20) FOREIGN KEY REFERENCES HoaDon(MaHoaDon),
    SoTienTra DECIMAL(18, 0) NOT NULL CHECK (SoTienTra > 0),
    NgayTra DATETIME DEFAULT GETDATE(),
    PhuongThuc NVARCHAR(50) CHECK (PhuongThuc IN (N'Chuyển khoản', N'Tiền mặt'))
);

CREATE TABLE SuCo (
    MaSuCo NVARCHAR(20) PRIMARY KEY,
    MaPhong NVARCHAR(20) FOREIGN KEY REFERENCES PhongTro(MaPhong),
    MaNguoiDung_Bao NVARCHAR(20) FOREIGN KEY REFERENCES NguoiDung(MaNguoiDung),
    MoTa NVARCHAR(500) NOT NULL,
    TrangThai NVARCHAR(30) DEFAULT N'Chờ xử lý' CHECK (TrangThai IN (N'Chờ xử lý', N'Đang sửa', N'Đã hoàn thành')),
    ChiPhiSua DECIMAL(18, 0) DEFAULT 0,
    NgayTao DATETIME DEFAULT GETDATE()
);

CREATE TABLE ThongBao (
    MaThongBao NVARCHAR(20) PRIMARY KEY,
    TieuDe NVARCHAR(200) NOT NULL,
    NoiDung NVARCHAR(MAX) NOT NULL,
    MaNguoiDung_Tao NVARCHAR(20) FOREIGN KEY REFERENCES NguoiDung(MaNguoiDung),
    NgayTao DATETIME DEFAULT GETDATE()
);
GO
-- Thêm cờ Xóa mềm cho các bảng quan trọng
ALTER TABLE KhachO ADD DaXoa BIT DEFAULT 0;
ALTER TABLE DichVu ADD DaXoa BIT DEFAULT 0;
ALTER TABLE HoaDon ADD DaXoa BIT DEFAULT 0;
ALTER TABLE ChiTietHoaDon ADD DaXoa BIT DEFAULT 0;
ALTER TABLE ThanhToan ADD DaXoa BIT DEFAULT 0;
ALTER TABLE SuCo ADD DaXoa BIT DEFAULT 0;
ALTER TABLE ThongBao ADD DaXoa BIT DEFAULT 0;
-- ==============================================
-- 2. DỮ LIỆU MẪU (SEED DATA LỖ GIC)
-- ==============================================

INSERT INTO VaiTro (MaVaiTro, TenVaiTro) VALUES 
('VT_ADMIN', N'Chủ trọ'), ('VT_TENANT', N'Khách thuê');

INSERT INTO NguoiDung (MaNguoiDung, TenDangNhap, MatKhau, MaVaiTro) VALUES 
('ND_ADMIN01', 'admin', '123456', 'VT_ADMIN'),
('ND_KHACH01', 'nguyenvana', '123456', 'VT_TENANT');

INSERT INTO PhongTro (MaPhong, TenPhong, DienTich, GiaThueCung, TrangThai) VALUES 
('P101', N'Phòng 101', 25.0, 3000000, N'Đang thuê'),
('P102', N'Phòng 102', 20.0, 2500000, N'Trống');

INSERT INTO HopDong (MaHopDong, MaPhong, MaNguoiDung_DaiDien, NgayBatDau, NgayKetThuc, TienCoc, TrangThai) VALUES 
('HD_P101_092026', 'P101', 'ND_KHACH01', '2026-09-01', '2027-09-01', 3000000, N'Hiệu lực');

INSERT INTO KhachO (MaKhach, MaHopDong, HoTen, CCCD, SoDienThoai, DaDangKyTamTru) VALUES 
('KH001', 'HD_P101_092026', N'Nguyễn Văn A', '001201123456', '0901234567', 1);

INSERT INTO DichVu (MaDichVu, TenDichVu, DonViTinh, DonGia, LaCoDinh) VALUES 
('DV_DIEN', N'Điện', N'kWh', 3500, 0),
('DV_NUOC', N'Nước', N'Khối', 25000, 0),
('DV_WIFI', N'Wifi', N'Tháng', 100000, 1);

INSERT INTO DichVu_HopDong (MaDV_HD, MaHopDong, MaDichVu) VALUES 
('DVHD_1', 'HD_P101_092026', 'DV_WIFI');

INSERT INTO ChiSoDienNuoc (MaChiSo, MaPhong, Thang, Nam, DienCu, DienMoi, NuocCu, NuocMoi) VALUES 
('CS_P101_092026', 'P101', 9, 2026, 100, 150, 10, 15);

INSERT INTO HoaDon (MaHoaDon, MaHopDong, Thang, Nam, TongTien, TrangThai) VALUES 
('HDON_P101_0926', 'HD_P101_092026', 9, 2026, 3400000, N'Đã thanh toán');

INSERT INTO ChiTietHoaDon (MaChiTiet, MaHoaDon, MaDichVu, SoLuong, DonGia, ThanhTien) VALUES 
('CT_1', 'HDON_P101_0926', 'DV_DIEN', 50, 3500, 175000),
('CT_2', 'HDON_P101_0926', 'DV_NUOC', 5, 25000, 125000);

INSERT INTO ThanhToan (MaThanhToan, MaHoaDon, SoTienTra, PhuongThuc) VALUES 
('TT_001', 'HDON_P101_0926', 3400000, N'Chuyển khoản');

INSERT INTO SuCo (MaSuCo, MaPhong, MaNguoiDung_Bao, MoTa, TrangThai) VALUES 
('SC_001', 'P101', 'ND_KHACH01', N'Hỏng bóng đèn nhà vệ sinh', N'Chờ xử lý');

INSERT INTO ThongBao (MaThongBao, TieuDe, NoiDung, MaNguoiDung_Tao) VALUES 
('TB_001', N'Lịch thu tiền tháng 9', N'Đề nghị các phòng hoàn tất thanh toán trước ngày 05/10', 'ND_ADMIN01');