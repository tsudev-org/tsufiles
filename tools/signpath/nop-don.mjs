// TsuFiles (chép từ tsucontact tools/signpath, AKB LES-20261122-2127-037b). Điền đơn SignPath Foundation (https://signpath.org/apply, biểu mẫu HubSpot) từ tools/signpath/don.json
// + ~/.config/tsudev/signpath-applicant.json (họ tên/email, NGOÀI repo).
// Mặc định: điền thử, chụp ảnh, KHÔNG gửi. Gửi thật: thêm --submit (chỉ khi đạt >= 4/6 ngưỡng, phiếu 20261113-02 mục 5).
// Chạy từ thư mục website/ (có playwright):  node ../tools/signpath/nop-don.mjs [--submit] [--headed] [--anh <tệp.png>]
// reCAPTCHA ẩn có thể chặn trình duyệt headless: lỗi gửi thì chạy lại với --headed.
import { readFileSync } from 'node:fs';
import { homedir } from 'node:os';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { createRequire } from 'node:module';

const require = createRequire(join(process.cwd(), 'package.json'));
const { chromium } = require('playwright');
const arg = process.argv.slice(2);
const guiThat = arg.includes('--submit');
const anh = arg.includes('--anh') ? arg[arg.indexOf('--anh') + 1] : 'signpath-don.png';
const thuMuc = dirname(fileURLToPath(import.meta.url));
const don = JSON.parse(readFileSync(join(thuMuc, 'don.json'), 'utf8'));
const nguoi = JSON.parse(readFileSync(join(homedir(), '.config/tsudev/signpath-applicant.json'), 'utf8'));
const REPO = 'tsudev-org/tsufiles';

// Số đo thật lúc chạy - không gõ tay.
const gh = async (u) => (await fetch(`https://api.github.com/${u}`, { headers: { 'User-Agent': 'tsufiles-signpath' } })).json();
const repo = await gh(`repos/${REPO}`);
const rel = (await gh(`repos/${REPO}/releases?per_page=100`)).filter((r) => !r.draft);
const sc = await (await fetch(`https://api.securityscorecards.dev/projects/github.com/${REPO}`)).json();
const wg = await fetch('https://api.github.com/repos/microsoft/winget-pkgs/contents/manifests/t/tsudev/TsuFiles', { headers: { 'User-Agent': 'tsufiles-signpath' } });
const so = {
  so_ban: rel.length,
  ban_moi: rel[0]?.tag_name ?? '',
  luot_tai: rel.flatMap((r) => r.assets).reduce((t, a) => t + a.download_count, 0),
  sao: repo.stargazers_count,
  scorecard: sc.score ?? 'n/a',
  winget: wg.ok ? ' and winget (winget install tsudev.TsuFiles)' : '',
};
const thay = (s) => s.replace(/\{\{(\w+)\}\}/g, (_, k) => String(so[k] ?? ''));
console.log('Số đo:', so);

const b = await chromium.launch({ headless: !arg.includes('--headed') });
const p = await b.newPage();
await p.goto('https://signpath.org/apply', { waitUntil: 'networkidle', timeout: 60000 });
await p.locator('#refuse-cookies-btn').click().catch(() => {});
const f = p.frames().find((x) => x.url().includes('hsforms'));
if (!f) throw new Error('Không thấy biểu mẫu HubSpot - trang đã đổi, kiểm tra lại.');

const o = (ten) => f.locator(`[name="${ten}"]`);
for (const k of ['name', 'repository_url', 'homepage_url', 'download_url', 'privacy_policy_url', 'wikipedia_url', 'tagline', 'description', 'reputation', 'discovery_source']) {
  if (don[k]) await o(`2-198696014/${k}`).fill(thay(don[k]));
}
for (const k of ['firstname', 'lastname', 'email', 'company']) await o(`0-1/${k}`).fill(nguoi[k] ?? '');
// Ô chọn (combobox): bấm ô hiển thị cạnh nhãn rồi chọn đúng chữ.
for (const [nhan, giaTri] of [['Category', don.category], ['Maintainer Type', don.maintainer], ['Build System', don.build_system], ['Primary Discovery Channel', don.discovery_channel]]) {
  const id = await f.locator('label', { hasText: nhan }).first().getAttribute('id');
  await f.locator(`[id="${id.replace(/-label$/, '-input')}"]`).click();
  await f.locator('[role=option]', { hasText: giaTri }).first().click();
}
const tich = async (ten, co) => { if (co) await o(ten).check({ force: true }); };
await tich('LEGAL_CONSENT.subscription_type_2099806800', don.dong_y.code_of_conduct);
await tich('LEGAL_CONSENT.subscription_type_408277728', don.dong_y.marketing);
await tich('LEGAL_CONSENT.processing', don.dong_y.processing);

// Kiểm lại giá trị đã điền (ô ẩn của combobox phải có giá trị).
for (const k of ['category', 'maintainer', 'build_system', 'discovery_channel']) {
  const v = await o(`2-198696014/${k}`).inputValue();
  if (!v) throw new Error(`Ô ${k} chưa chọn được`);
}
await p.screenshot({ path: anh, fullPage: true });
console.log('Đã điền, ảnh:', anh);

if (guiThat) {
  await f.locator('button[type=submit]', { hasText: 'Submit' }).click();
  await p.waitForTimeout(8000);
  const ketQua = (await f.locator('body').innerText()).slice(0, 400);
  await p.screenshot({ path: anh.replace(/\.png$/, '-sau-gui.png'), fullPage: true });
  console.log('Sau khi gửi:', ketQua);
} else {
  console.log('CHƯA gửi (thiếu --submit).');
}
await b.close();
