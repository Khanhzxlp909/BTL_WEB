using BookStore.Web.Domain;
using BookStore.Web.Infrastructure;
using BookStore.Web.Models;
using BookStore.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BookStore.Web.Controllers;

[Authorize]
public sealed class CartController(ICartService carts) : Controller
{
    [HttpGet("/gio-hang")]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await carts.GetAsync(CommerceSupport.UserId(User), ct));
}

[ApiController, Authorize, Route("api/cart")]
public sealed class CartApiController(ICartService carts) : ControllerBase
{
    private int UserId => CommerceSupport.UserId(User);
    [HttpGet] public Task<CartDto> Get(CancellationToken ct) => carts.GetAsync(UserId, ct);
    [HttpPost("items")] public Task<CartDto> Add(CartAddInput input, CancellationToken ct) => carts.AddAsync(UserId, input, ct);
    [HttpPut("items/{id:int}")] public Task<CartDto> Update(int id, CartQuantityInput input, CancellationToken ct) => carts.UpdateAsync(UserId, id, input.Quantity, ct);
    [HttpDelete("items/{id:int}")] public Task<CartDto> Delete(int id, CancellationToken ct) => carts.UpdateAsync(UserId, id, 0, ct);
    [HttpDelete] public Task<CartDto> Clear(CancellationToken ct) => carts.ClearAsync(UserId, ct);
}

[Authorize]
public sealed class CheckoutController(IOrderService orders) : Controller
{
    [HttpGet("/dat-hang")]
    public async Task<IActionResult> Index(CancellationToken ct) => View(await orders.CheckoutAsync(CommerceSupport.UserId(User), ct));
    [HttpPost("/dat-hang")]
    public async Task<IActionResult> Index([Bind(Prefix = "Input")] CheckoutInput input, CancellationToken ct)
    {
        var userId = CommerceSupport.UserId(User);
        if (ModelState.IsValid)
            try
            {
                var order = await orders.PlaceAsync(userId, input, ct);
                return Redirect($"/dat-hang/thanh-cong/{order.Id}");
            }
            catch (AppException ex) when (ex.StatusCode is 400 or 409) { AdminErrors.Apply(this, ex, "Input."); }
        else Response.StatusCode = 400;
        var fresh = await orders.CheckoutAsync(userId, ct);
        input.QuoteToken = fresh.Input.QuoteToken;
        input.ConfirmQuote = false;
        ModelState.Remove("Input.QuoteToken"); ModelState.Remove("Input.ConfirmQuote");
        return View(new CheckoutViewModel(fresh.Cart, input));
    }

    // Trang kết quả chỉ đọc đơn chính chủ; tải lại trang không tạo đơn mới.
    [HttpGet("/dat-hang/thanh-cong/{id:int}")]
    public async Task<IActionResult> Success(int id, CancellationToken ct)
    {
        var order = await orders.DetailAsync(CommerceSupport.UserId(User), false, id, ct);
        return View(new OrderPlacedViewModel(order.Id, order.OrderCode, order.TotalAmount, order.PaymentMethod, order.CreatedAt));
    }
}

[Authorize]
public sealed class OrdersController(IOrderService orders) : Controller
{
    [HttpGet("/don-hang")]
    public async Task<IActionResult> Index([FromQuery] OrderQuery query, CancellationToken ct)
    {
        if (ModelState.IsValid) return View(await orders.ListAsync(CommerceSupport.UserId(User), false, query, ct));
        Response.StatusCode = 400;
        return View(new OrderListViewModel(query, new([], 1, 12, 0), false));
    }
    [HttpGet("/don-hang/{id:int}")]
    public async Task<IActionResult> Detail(int id, CancellationToken ct) => View(new OrderDetailViewModel(await orders.DetailAsync(CommerceSupport.UserId(User), false, id, ct), false));
    [HttpPost("/don-hang/{id:int}/huy")]
    public async Task<IActionResult> Cancel(int id, CancelOrderInput input, CancellationToken ct)
    {
        if (ModelState.IsValid)
            try
            {
                await orders.CancelAsync(CommerceSupport.UserId(User), false, id, input, ct);
                TempData["Success"] = ValidationText.Pick("Đơn đã hủy; giữ chỗ chỉ được hoàn một lần.", "The order is cancelled; its reservation is released only once.");
                return Redirect($"/don-hang/{id}");
            }
            catch (AppException ex) when (ex.StatusCode is 400 or 409) { AdminErrors.Apply(this, ex); }
        else Response.StatusCode = 400;
        return View("Detail", new OrderDetailViewModel(await orders.DetailAsync(CommerceSupport.UserId(User), false, id, ct), false));
    }
}

[Authorize(Roles = RoleNames.Admin), Route("admin/don-hang")]
public sealed class AdminOrdersController(IOrderService orders) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] OrderQuery query, CancellationToken ct)
    {
        if (ModelState.IsValid) return View(await orders.ListAsync(CommerceSupport.UserId(User), true, query, ct));
        Response.StatusCode = 400;
        return View(new OrderListViewModel(query, new([], 1, 12, 0), true));
    }
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Detail(int id, CancellationToken ct) => View(new OrderDetailViewModel(await orders.DetailAsync(CommerceSupport.UserId(User), true, id, ct), true));
    [HttpPost("{id:int}/trang-thai")]
    public Task<IActionResult> Transition(int id, OrderStatusInput input, CancellationToken ct) => Write(id,
        () => orders.TransitionAsync(CommerceSupport.UserId(User), id, input, ct), ct);
    [HttpPost("{id:int}/huy")]
    public Task<IActionResult> Cancel(int id, CancelOrderInput input, CancellationToken ct) => Write(id,
        () => orders.CancelAsync(CommerceSupport.UserId(User), true, id, input, ct), ct);
    private async Task<IActionResult> Write(int id, Func<Task> action, CancellationToken ct)
    {
        if (ModelState.IsValid)
            try
            {
                await action();
                TempData["Success"] = ValidationText.Pick("Đã xử lý trạng thái đơn; yêu cầu lặp không làm thay đổi tồn lần hai.", "Order status processed; repeated requests do not change inventory twice.");
                return Redirect($"/admin/don-hang/{id}");
            }
            catch (AppException ex) when (ex.StatusCode is 400 or 409) { AdminErrors.Apply(this, ex); }
        else Response.StatusCode = 400;
        return View("Detail", new OrderDetailViewModel(await orders.DetailAsync(CommerceSupport.UserId(User), true, id, ct), true));
    }
}

[ApiController, Authorize, Route("api/orders")]
public sealed class OrdersApiController(IOrderService orders) : ControllerBase
{
    private int UserId => CommerceSupport.UserId(User);
    [HttpGet] public Task<OrderListViewModel> List([FromQuery] OrderQuery query, CancellationToken ct) => orders.ListAsync(UserId, false, query, ct);
    [HttpGet("{id:int}")] public Task<OrderDetailDto> Detail(int id, CancellationToken ct) => orders.DetailAsync(UserId, false, id, ct);
    [HttpGet("quote")] public Task<CheckoutViewModel> Quote(CancellationToken ct) => orders.CheckoutAsync(UserId, ct);
    [HttpPost] public async Task<IActionResult> Place(CheckoutInput input, CancellationToken ct)
    { var order = await orders.PlaceAsync(UserId, input, ct); return Created($"/api/orders/{order.Id}", order); }
    [HttpPost("{id:int}/cancel")] public async Task<IActionResult> Cancel(int id, CancelOrderInput input, CancellationToken ct)
    { await orders.CancelAsync(UserId, false, id, input, ct); return Ok(await orders.DetailAsync(UserId, false, id, ct)); }

    // Đường dẫn theo BA: tra cứu và hủy bằng mã đơn, luôn giới hạn trong đơn của người gọi.
    [HttpGet("{orderCode:regex(^DH[[0-9a-f]]+$)}")]
    public async Task<OrderDetailDto> DetailByCode(string orderCode, CancellationToken ct)
        => await orders.DetailAsync(UserId, false, await orders.ResolveCodeAsync(UserId, orderCode, ct), ct);
    [HttpPost("{orderCode:regex(^DH[[0-9a-f]]+$)}/cancellation")]
    public async Task<OrderDetailDto> CancelByCode(string orderCode, CancelOrderInput input, CancellationToken ct)
    {
        var id = await orders.ResolveCodeAsync(UserId, orderCode, ct);
        await orders.CancelAsync(UserId, false, id, input, ct);
        return await orders.DetailAsync(UserId, false, id, ct);
    }
}

[ApiController, Authorize(Roles = RoleNames.Admin), Route("api/admin/orders")]
public sealed class AdminOrdersApiController(IOrderService orders) : ControllerBase
{
    private int UserId => CommerceSupport.UserId(User);
    [HttpGet] public Task<OrderListViewModel> List([FromQuery] OrderQuery query, CancellationToken ct) => orders.ListAsync(UserId, true, query, ct);
    [HttpPut("{id:int}/status")] public async Task<IActionResult> Transition(int id, OrderStatusInput input, CancellationToken ct)
    { await orders.TransitionAsync(UserId, id, input, ct); return Ok(await orders.DetailAsync(UserId, true, id, ct)); }
    [HttpPost("{id:int}/cancel")] public async Task<IActionResult> Cancel(int id, CancelOrderInput input, CancellationToken ct)
    { await orders.CancelAsync(UserId, true, id, input, ct); return Ok(await orders.DetailAsync(UserId, true, id, ct)); }
}
