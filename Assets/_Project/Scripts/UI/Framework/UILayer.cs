/// <summary>
/// Lớp hiển thị của UI. Thứ tự trong enum cũng là thứ tự vẽ: lớp sau nằm trên lớp trước.
/// </summary>
public enum UILayer
{
    Background = 0,   // nền toàn màn hình
    Content = 1,      // màn hình chính (menu, cửa hàng...)
    Popup = 2,        // hộp thoại, cửa sổ bật lên
    Overlay = 3       // trên cùng: thông báo, loading, hướng dẫn
}
