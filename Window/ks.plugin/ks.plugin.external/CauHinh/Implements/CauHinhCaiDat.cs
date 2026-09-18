using ks.plugin.external.CauHinh.Interfaces;
using ks.plugin.shared.Constants;
using Microsoft.Win32;

namespace ks.plugin.external.CauHinh.Implements
{
    public class CauHinhCaiDat : ICauHinhCaiDat
    {
        public string? DocMoiTruong()
        {
            using var khoa = Registry.CurrentUser.OpenSubKey(CaiDatConstants.KhoaCauHinh);
            var moiTruong = khoa?.GetValue(CaiDatConstants.GiaTriMoiTruong) as string;

            return string.IsNullOrWhiteSpace(moiTruong) ? null : moiTruong;
        }
    }
}
