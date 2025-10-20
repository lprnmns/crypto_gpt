const nextConfig = {
  reactStrictMode: true,
  experimental: {
    typedRoutes: true,
    serverActions: {
      allowedOrigins: ["*"],
    },
  },
};

export default nextConfig;

