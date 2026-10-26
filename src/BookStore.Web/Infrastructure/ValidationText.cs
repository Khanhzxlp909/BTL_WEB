using System.Globalization;

namespace BookStore.Web.Infrastructure;

public static class ValidationText
{
    public static string Pick(string vi, string en) => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en" ? en : vi;
    public static string Required => Pick("Trường này bắt buộc. Hãy nhập dữ liệu trước khi gửi.", "This field is required. Enter a value before submitting.");
    public static string MaxLength => Pick("Trường {0} vượt quá {1} ký tự. Hãy rút ngắn dữ liệu.", "{0} exceeds {1} characters. Shorten the value.");
    public static string UserName => Pick("Tên đăng nhập cần 3–50 ký tự chữ không dấu, số hoặc gạch dưới.", "Use 3–50 letters, numbers or underscores for your username.");
    public static string Email => Pick("Email không đúng định dạng. Hãy nhập địa chỉ email hợp lệ.", "The email format is invalid. Enter a valid email address.");
    public static string PasswordLength => Pick("Mật khẩu cần 10–64 ký tự; khoảng trắng được giữ nguyên.", "Use a password of 10–64 characters; spaces are preserved.");
    public static string PasswordMatch => Pick("Xác nhận mật khẩu không khớp. Hãy nhập lại đúng mật khẩu.", "The passwords do not match. Enter the same password again.");
    public static string Phone => Pick("Số điện thoại cần 7–20 ký tự, gồm số và dấu +, -, ngoặc hoặc khoảng trắng.", "Use a phone number of 7–20 characters: digits, +, -, parentheses or spaces.");
    public static string Positive => Pick("Trường {0} phải là số nguyên từ 1 trở lên.", "{0} must be an integer of at least 1.");
    public static string Price => Pick("Giá lọc phải là số nguyên đồng không âm. Hãy nhập lại giá hợp lệ.", "The price filter must be a non-negative whole VND amount.");
    public static string PriceRange => Pick("Giá cao nhất phải lớn hơn hoặc bằng giá thấp nhất. Hãy sửa khoảng giá.", "The maximum price must be at least the minimum price.");
    public static string PageSize => Pick("Mỗi trang cần từ 1 đến 100 sách. Hãy sửa số mục mỗi trang.", "Choose between 1 and 100 books per page.");
    public static string Sort => Pick("Cách sắp xếp không hợp lệ. Chọn newest, price-asc, price-desc hoặc title.", "Choose a valid sort order: newest, price-asc, price-desc or title.");
}
