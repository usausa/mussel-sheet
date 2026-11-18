// The same names as SupportedCultures.Names, so a culture the app does not support never sets lang
const exampleCultures = ['en-US', 'ja-JP'];

// Example.Client reads this when it starts in the browser, so WebAssembly uses the same language as the server
window.exampleCulture = () => {
    const cookie = document.cookie.split('; ').find(x => x.startsWith('.AspNetCore.Culture='));
    const value = cookie ? decodeURIComponent(cookie.substring(cookie.indexOf('=') + 1)) : '';
    return /(?:^|\|)c=([^|]*)/.exec(value)?.[1] ?? null;
};

document.documentElement.lang = (exampleCultures.find(x => x === window.exampleCulture()) ?? exampleCultures[0]).split('-')[0];

window.exampleSaveCulture = name => {
    const value = encodeURIComponent(`c=${name}|uic=${name}`);
    document.cookie = `.AspNetCore.Culture=${value}; path=${new URL(document.baseURI).pathname}; max-age=2592000; samesite=lax`;
    location.reload();
};
