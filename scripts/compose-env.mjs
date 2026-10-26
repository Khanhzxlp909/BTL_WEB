import { randomBytes } from 'node:crypto';
import { writeFile } from 'node:fs/promises';
import { resolve } from 'node:path';

const destination = resolve(import.meta.dirname, '..', '.env');
try {
  const password = () => `Bs!${randomBytes(24).toString('hex')}A1`;
  await writeFile(destination, [
    `BOOKSTORE_SQL_PASSWORD=${password()}`,
    'BOOKSTORE_ADMIN_USERNAME=bookstore_admin',
    'BOOKSTORE_ADMIN_EMAIL=admin@example.test',
    `BOOKSTORE_ADMIN_PASSWORD=${password()}`,
    'BOOKSTORE_HTTP_PORT=5087',
    '',
  ].join('\n'), { mode: 0o600, flag: 'wx' });
  console.info('Đã tạo .env local với mật khẩu ngẫu nhiên, quyền 0600. Xem thông tin đăng nhập trong .env; không đưa tệp này vào Git hoặc tài liệu.');
} catch (cause) {
  console.error(cause?.code === 'EEXIST'
    ? 'COMPOSE_ENV_EXISTS: .env đã tồn tại, chưa thay đổi nội dung. Dùng cấu hình hiện có hoặc tự chỉnh .env.'
    : `COMPOSE_ENV_CREATE_FAILED: Không thể tạo .env (${cause?.code ?? 'unknown'}). Kiểm tra quyền ghi thư mục dự án rồi chạy lại pnpm compose:env.`);
  process.exitCode = 1;
}
