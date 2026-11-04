// Example.Client reads this when it starts in the browser, so WebAssembly uses the same language as the server
window.exampleCulture = () => {
    const cookie = document.cookie.split('; ').find(x => x.startsWith('.AspNetCore.Culture='));
    const value = cookie ? decodeURIComponent(cookie.substring(cookie.indexOf('=') + 1)) : '';
    return /(?:^|\|)c=([^|]*)/.exec(value)?.[1] ?? null;
};
