namespace DomainLayer.Enums;

/// <summary>
/// Trạng thái của người dùng
/// </summary>
public enum UserStatus
{
    /// <summary>Đang hoạt động bình thường</summary>
    Active,
    /// <summary>Tạm khóa hoặc chưa kích hoạt</summary>
    Inactive,
    /// <summary>Bị đình chỉ (banned) do vi phạm</summary>
    Suspended
}

/// <summary>
/// Trạng thái của khu chợ đêm
/// </summary>
public enum NightMarketStatus
{
    /// <summary>Chợ đang mở cửa hoạt động</summary>
    Active,
    /// <summary>Chợ tạm thời đóng cửa</summary>
    Inactive,
    /// <summary>Chợ đã giải thể/đóng vĩnh viễn</summary>
    Closed
}

/// <summary>
/// Trạng thái của gian hàng
/// </summary>
public enum BoothStatus
{
    /// <summary>Đang chờ Ban quản lý duyệt</summary>
    Pending,
    /// <summary>Đang hoạt động bán hàng</summary>
    Active,
    /// <summary>Chủ quán tạm nghỉ bán</summary>
    Inactive,
    /// <summary>Bị đình chỉ do vi phạm quy định</summary>
    Suspended
}

/// <summary>
/// Trạng thái của đơn hàng (Quy trình: Đặt -> Chuẩn bị -> Sẵn sàng -> Hoàn tất/Hủy)
/// </summary>
public enum OrderStatus
{
    /// <summary>Đơn mới đặt, chờ quán xác nhận</summary>
    Pending,
    /// <summary>Quán đã nhận và đang chế biến món ăn</summary>
    Processing,
    /// <summary>Món đã làm xong, sẵn sàng giao hoặc chờ khách tới lấy</summary>
    Ready,
    /// <summary>Giao dịch hoàn tất, khách đã nhận món</summary>
    Completed,
    /// <summary>Đơn hàng bị hủy bỏ</summary>
    Cancelled
}

/// <summary>
/// Trạng thái thanh toán
/// </summary>
public enum PaymentStatus
{
    /// <summary>Chờ khách hàng thanh toán</summary>
    Pending,
    /// <summary>Thanh toán thành công</summary>
    Success,
    /// <summary>Giao dịch thất bại hoặc bị lỗi</summary>
    Failed,
    /// <summary>Đã hoàn tiền lại cho khách</summary>
    Refunded
}

/// <summary>
/// Trạng thái của đơn khiếu nại
/// </summary>
public enum ComplaintStatus
{
    /// <summary>Khiếu nại mới gửi, chờ tiếp nhận</summary>
    Pending,
    /// <summary>Ban quản lý đang xem xét và giải quyết</summary>
    Processing,
    /// <summary>Khiếu nại đã được xử lý thỏa đáng</summary>
    Resolved,
    /// <summary>Khiếu nại không hợp lệ, bị từ chối giải quyết</summary>
    Rejected
}

/// <summary>
/// Trạng thái của đơn đăng ký mở gian hàng
/// </summary>
public enum RegistrationStatus
{
    /// <summary>Đang chờ xem xét hồ sơ đăng ký</summary>
    Pending,
    /// <summary>Đã được chấp thuận, cấp quyền mở gian hàng</summary>
    Approved,
    /// <summary>Đơn đăng ký bị từ chối do không đạt yêu cầu</summary>
    Rejected
}

/// <summary>
/// Trạng thái của gói dịch vụ/quảng cáo
/// </summary>
public enum PackageStatus
{
    /// <summary>Gói đang được mở bán/áp dụng</summary>
    Active,
    /// <summary>Gói đã ngừng cung cấp</summary>
    Inactive
}

/// <summary>
/// Trạng thái của khu vực (Zone) trong chợ đêm
/// </summary>
public enum ZoneStatus
{
    /// <summary>Khu vực đang mở cho thuê/hoạt động bình thường</summary>
    Active,
    /// <summary>Khu vực đang sửa chữa hoặc tạm đóng</summary>
    Inactive
}

/// <summary>
/// Trạng thái của chương trình khuyến mãi (Promotion)
/// </summary>
public enum PromotionStatus
{
    /// <summary>Khuyến mãi đang trong thời gian diễn ra</summary>
    Active,
    /// <summary>Khuyến mãi đã quá thời hạn áp dụng</summary>
    Expired,
    /// <summary>Bị tắt/vô hiệu hóa thủ công bởi gian hàng</summary>
    Disabled
}

/// <summary>
/// Loại tin nhắn trong chat (SignalR)
/// </summary>
public enum MessageType
{
    /// <summary>Tin nhắn văn bản thuần túy</summary>
    Text,
    /// <summary>Tin nhắn chứa hình ảnh</summary>
    Image,
    /// <summary>Tin nhắn tự động sinh ra từ hệ thống</summary>
    System
}

/// <summary>
/// Phân loại thông báo (Notification)
/// </summary>
public enum NotificationType
{
    /// <summary>Thông báo chung từ hệ thống/chợ đêm</summary>
    System,
    /// <summary>Thông báo cập nhật tình trạng đơn hàng</summary>
    Order,
    /// <summary>Thông báo nhắc nhở về voucher/khuyến mãi</summary>
    Promotion,
    /// <summary>Thông báo mang tính cảnh báo/nhắc nhở vi phạm</summary>
    Alert
}

/// <summary>
/// Trạng thái gói cước (đăng ký) của gian hàng
/// </summary>
public enum SubscriptionStatus
{
    /// <summary>Gói đăng ký đang còn thời hạn sử dụng</summary>
    Active,
    /// <summary>Gói đăng ký đã hết hạn</summary>
    Expired,
    /// <summary>Gian hàng đã hủy gói đăng ký trước hạn</summary>
    Cancelled
}

/// <summary>
/// Hình thức thanh toán của giao dịch
/// </summary>
public enum PaymentType
{
    /// <summary>Tiền mặt (thường trả trực tiếp tại quầy)</summary>
    Cash,
    /// <summary>Thanh toán trực tuyến qua PayOS</summary>
    PayOS
}

/// <summary>
/// Trạng thái xác minh của giấy tờ
/// </summary>
public enum VerificationStatus
{
    /// <summary>Chờ xác minh</summary>
    Pending,
    /// <summary>Đã xác minh hợp lệ</summary>
    Verified,
    /// <summary>Bị từ chối (không hợp lệ)</summary>
    Rejected
}
