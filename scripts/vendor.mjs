import { cp, mkdir } from 'node:fs/promises';
import { createRequire } from 'node:module';
import { dirname, resolve } from 'node:path';

const require = createRequire(import.meta.url);
const root = resolve(import.meta.dirname, '..');
const output = resolve(root, 'src/BookStore.Web/wwwroot/lib');
try {
  for (const [source, target] of [
    ['bootstrap/dist/css/bootstrap.min.css', 'bootstrap/bootstrap.min.css'],
    ['bootstrap/dist/js/bootstrap.bundle.min.js', 'bootstrap/bootstrap.bundle.min.js'],
    ['bootstrap/LICENSE', 'bootstrap/LICENSE'],
    ['jquery/dist/jquery.min.js', 'jquery/jquery.min.js'],
    ['jquery/LICENSE.txt', 'jquery/LICENSE.txt'],
  ]) {
    const destination = resolve(output, target);
    await mkdir(dirname(destination), { recursive: true });
    await cp(require.resolve(source), destination);
  }
  console.info('Đã sao chép Bootstrap và jQuery vào wwwroot/lib; trình duyệt không cần CDN.');
} catch (cause) {
  const code = cause?.code ?? 'VENDOR_COPY_FAILED';
  console.error(`VENDOR_COPY_FAILED: Không thể sao chép thư viện giao diện (${code}). Chạy pnpm install, kiểm tra quyền ghi wwwroot/lib rồi chạy pnpm vendor.`, { cause });
  process.exitCode = 1;
}
