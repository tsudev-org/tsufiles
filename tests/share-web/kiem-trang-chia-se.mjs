// Kiểm thử đầu-cuối trang "Gửi và nhận tệp" (ADR-0004 S2d) bằng WebKit giả lập iPhone 8 (Safari 16).
//
// Chạy `tsufiles-cli share` THẬT trong một pty (lệnh `script` của util-linux) để đi đúng luồng người dùng: máy tính in
// địa chỉ, điện thoại mở trang, dòng lệnh hỏi "Đồng ý? [y/N]" và ta trả lời "y". Sau đó điện thoại tải tệp máy tính
// mời, gửi một tệp lên (so từng byte), thử gửi tệp .exe (phải bị chặn), và kiểm trang không lỗi script/CSP, không
// cuộn ngang ở bề rộng 375 px.
//
// Dùng:  node tests/share-web/kiem-trang-chia-se.mjs
//   TSUFILES_CLI   đường dẫn tệp chạy tsufiles-cli (mặc định bản Release đã build)
//   TRINH_DUYET    webkit (mặc định) | chromium - chromium chỉ để thử trên máy thiếu thư viện của WebKit
//   ANH_CHUP       thư mục lưu ảnh chụp màn hình (tùy chọn)
// Playwright lấy từ website/node_modules (cd website && npm ci && npx playwright install --with-deps webkit).

import { spawn } from 'node:child_process';
import { createRequire } from 'node:module';
import { mkdtempSync, writeFileSync, readFileSync, readdirSync, rmSync, mkdirSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const goc = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const require = createRequire(join(goc, 'website', 'package.json'));
const { webkit, chromium, devices } = require('playwright');

const cli = process.env.TSUFILES_CLI ?? join(goc, 'src/TsuFiles.Cli/bin/Release/net10.0/tsufiles-cli');
const loai = process.env.TRINH_DUYET ?? 'webkit';
const trinhDuyet = { webkit, chromium }[loai];
if (!trinhDuyet) throw new Error(`TRINH_DUYET không hợp lệ: ${loai}`);

const loi = [];
const kiem = (dung, moTa) => {
  console.log(`${dung ? 'ĐẠT ' : 'TRƯỢT'} ${moTa}`);
  if (!dung) loi.push(moTa);
};

const tam = mkdtempSync(join(tmpdir(), 'tsufiles-share-'));
const thuMucNhan = join(tam, 'nhan');
mkdirSync(thuMucNhan);
const tenMoi = 'báo cáo tháng 11.txt';
const noiDungMoi = 'Tệp máy tính mời điện thoại tải về - có dấu tiếng Việt.\n';
writeFileSync(join(tam, tenMoi), noiDungMoi);

// `script -q -f -e -c <lệnh> /dev/null`: chạy lệnh trong pty, -e trả mã thoát của lệnh, -f đẩy ra ngay.
const lenh = [cli, 'share', join(tam, tenMoi), '-o', thuMucNhan].map((s) => `'${s.replaceAll("'", "'\\''")}'`).join(' ');
const tienTrinh = spawn('script', ['-qfec', lenh, '/dev/null'], {
  env: { ...process.env, TSUFILES_LANG: 'vi', DOTNET_NOLOGO: '1' },
  stdio: ['pipe', 'pipe', 'pipe'],
});
let dauRa = '';
const choDauRa = (mau, ms = 60_000) =>
  new Promise((ok, hong) => {
    const hen = setTimeout(() => hong(new Error(`Hết giờ chờ "${mau}". Đầu ra:\n${dauRa}`)), ms);
    const xem = () => {
      const m = dauRa.match(mau);
      if (m) {
        clearTimeout(hen);
        tienTrinh.stdout.off('data', xem);
        ok(m);
      }
    };
    tienTrinh.stdout.on('data', xem);
    xem();
  });
tienTrinh.stdout.on('data', (d) => (dauRa += d.toString()));
tienTrinh.stderr.on('data', (d) => (dauRa += d.toString()));

let trinh;
try {
  const [diaChi] = await choDauRa(/http:\/\/[\d.]+:\d+\/s\/[0-9a-f]{32}\//);
  console.log(`Địa chỉ phiên: ${diaChi.replace(/[0-9a-f]{32}/, '<mã phiên>')}`);

  // Khi điện thoại mở trang, dòng lệnh hỏi duyệt; trả lời "y" như người dùng.
  choDauRa(/Đồng ý\? \[y\/N\]/).then(() => tienTrinh.stdin.write('y\n'), () => {});

  trinh = await trinhDuyet.launch();
  const ngucanh = await trinh.newContext({ ...devices['iPhone 8'], acceptDownloads: true });
  const trang = await ngucanh.newPage();
  // 415 là câu trả lời dự kiến khi gửi tệp .exe ở dưới; mọi lỗi console khác (gồm vi phạm CSP) đều tính.
  trang.on('console', (m) => m.type() === 'error' && !/status of 415/.test(m.text()) && loi.push(`console: ${m.text()}`));
  trang.on('pageerror', (e) => loi.push(`pageerror: ${e.message}`));

  const phanHoi = await trang.goto(diaChi, { timeout: 60_000 });
  kiem(phanHoi?.status() === 200, 'mở trang sau khi máy tính đồng ý (200)');
  const csp = phanHoi?.headers()['content-security-policy'] ?? '';
  kiem(csp.includes("default-src 'none'") && csp.includes("script-src 'self'"), 'có CSP chặt');
  kiem((await trang.title()).startsWith('TsuFiles'), 'tiêu đề trang');
  kiem(/muốn kết nối/.test(dauRa), 'dòng lệnh đã hỏi duyệt thiết bị');

  const rongCuon = await trang.evaluate(() => document.documentElement.scrollWidth - window.innerWidth);
  kiem(rongCuon <= 0, `không cuộn ngang ở bề rộng ${devices['iPhone 8'].viewport.width} px (lệch ${rongCuon})`);

  // Tải tệp máy tính mời.
  const lienKet = trang.getByRole('link', { name: tenMoi });
  kiem((await lienKet.count()) === 1, 'liệt kê tệp máy tính mời');
  const [taiVe] = await Promise.all([trang.waitForEvent('download'), lienKet.click()]);
  kiem(taiVe.suggestedFilename() === tenMoi, `tên tệp tải về giữ dấu (${taiVe.suggestedFilename()})`);
  const duongTai = join(tam, 'tai-ve.bin');
  await taiVe.saveAs(duongTai);
  kiem(readFileSync(duongTai, 'utf8') === noiDungMoi, 'nội dung tệp tải về khớp');

  // Gửi một tệp lên máy tính (giả ảnh HEIC của iPhone), so từng byte.
  const anh = Buffer.alloc(3 * 1024 * 1024 + 17);
  for (let i = 0; i < anh.length; i++) anh[i] = (i * 31 + 7) & 0xff;
  await trang.setInputFiles('#chon', { name: 'IMG_0001 ảnh.HEIC', mimeType: 'image/heic', buffer: anh });
  await trang.getByText(/^(Đã gửi|Lỗi|Mất kết nối)/).first().waitFor({ timeout: 60_000 });
  kiem(await trang.getByText('Đã gửi: IMG_0001 ảnh.HEIC').isVisible(), 'trang báo đã gửi');
  await choDauRa(/Đã nhận: .*IMG_0001 ảnh\.HEIC/);
  kiem(readFileSync(join(thuMucNhan, 'IMG_0001 ảnh.HEIC')).equals(anh), 'tệp nhận khớp từng byte (3 MB)');

  // Tệp chạy được phải bị chặn, không ghi gì vào thư mục nhận.
  await trang.setInputFiles('#chon', { name: 'cai-dat.exe', mimeType: 'application/octet-stream', buffer: Buffer.from('MZ') });
  await trang.getByText(/^Lỗi: /).waitFor({ timeout: 30_000 });
  kiem(!readdirSync(thuMucNhan).some((t) => t.toLowerCase().endsWith('.exe')), 'chặn tệp .exe');
  kiem(readdirSync(thuMucNhan).every((t) => !t.endsWith('.part')), 'không còn tệp tạm .part');

  // Sai mã phiên: 404, không lộ trang.
  const sai = await ngucanh.request.get(diaChi.replace(/[0-9a-f]{32}/, '0'.repeat(32)));
  kiem(sai.status() === 404, 'sai mã phiên trả 404');

  if (process.env.ANH_CHUP) {
    mkdirSync(process.env.ANH_CHUP, { recursive: true });
    await trang.screenshot({ path: join(process.env.ANH_CHUP, `trang-chia-se-${loai}.png`), fullPage: true });
  }
  kiem(!loi.some((l) => l.startsWith('console') || l.startsWith('pageerror')), 'không lỗi script/CSP trên trang');
} catch (e) {
  loi.push(e.message);
  console.error(e);
} finally {
  await trinh?.close();
  tienTrinh.kill('SIGINT');
  rmSync(tam, { recursive: true, force: true });
}

if (loi.length) {
  console.error(`\n${loi.length} lỗi:\n- ${loi.join('\n- ')}`);
  process.exit(1);
}
console.log(`\nTrang chia sẻ đạt trên ${loai} (iPhone 8).`);
