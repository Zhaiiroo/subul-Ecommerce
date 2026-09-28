import { Suspense } from 'react';
import { PageContainer } from '@/components/layout/page-container';
import { AdminUserListingPage } from '@/features/admin-user';
import { messages } from '@/lib/messages.ar';

export const metadata = {
  title: messages.adminUser.listing.title,
};

export default function AdminUsersPage() {
  return (
    <PageContainer scrollable={false}>
      <Suspense>
        <AdminUserListingPage />
      </Suspense>
    </PageContainer>
  );
}
